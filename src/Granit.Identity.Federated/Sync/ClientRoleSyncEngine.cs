using System.Diagnostics.CodeAnalysis;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.Sync;

/// <summary>
/// Provider-agnostic client-role sync: enumerates a provider's client-scope roles for every tracked
/// client and upserts matching role-metadata rows, then applies the orphan policy (ADR-029).
/// </summary>
public interface IClientRoleSyncEngine
{
    /// <summary>Runs the client-role sync for one provider, described by <paramref name="policy"/>.</summary>
    Task SyncAsync(IClientRoleSyncPolicy policy, CancellationToken cancellationToken);
}

/// <summary>
/// Provider-agnostic client-role sync: enumerates a provider's client-scope roles (via the
/// <see cref="IIdentityClientRoleManager"/> seam) for every tracked client and upserts matching
/// <see cref="RoleMetadata"/> rows, then applies the orphan policy (ADR-029) to rows the provider
/// no longer returns. This is the single implementation shared by every federated provider; the
/// per-provider slice (tracked clients, enablement, exception classification) lives in an
/// <see cref="IClientRoleSyncPolicy"/>.
/// </summary>
/// <remarks>
/// Failure policy: log &amp; skip. A provider unreachable, a service account missing permissions,
/// or a tracked client not present upstream each produces a log entry and moves on — the host
/// never crashes on sync failure; the next run is a natural retry. Roles are host-scope, persisted
/// with <c>TenantId = null</c> and <c>ClientId = the provider's client id</c>. Restore-on-return is
/// automatic: a previously orphaned row that reappears upstream has its orphan flag cleared.
/// </remarks>
public sealed partial class ClientRoleSyncEngine(
    IIdentityClientRoleManager clientRoleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILogger<ClientRoleSyncEngine> logger) : IClientRoleSyncEngine
{
    /// <inheritdoc/>
    public async Task SyncAsync(IClientRoleSyncPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!policy.Enabled)
        {
            LogDisabled(logger, policy.ProviderName);
            return;
        }

        if (policy.TrackedClientIds.Count == 0)
        {
            LogNoTrackedClients(logger, policy.ProviderName);
            return;
        }

        foreach (string clientId in policy.TrackedClientIds)
        {
            await SyncClientAsync(policy, clientId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncClientAsync(
        IClientRoleSyncPolicy policy, string clientId, CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityRole> roles;
        try
        {
            roles = await clientRoleManager.GetClientRolesAsync(clientId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (policy.ClassifyFetchFault(ex) is { } fault)
        {
            switch (fault)
            {
                case ClientRoleFetchFault.ClientNotFound:
                    LogClientNotFound(logger, policy.ProviderName, clientId);
                    break;
                case ClientRoleFetchFault.Forbidden:
                    LogForbidden(logger, ex, policy.ProviderName, clientId, policy.ForbiddenRemediation);
                    break;
                default:
                    LogClientSyncFailed(logger, ex, policy.ProviderName, clientId);
                    break;
            }

            return;
        }

        int added = 0;
        int updated = 0;
        int unchanged = 0;
        int restored = 0;

        HashSet<string> providerRoleNames = new(roles.Count, StringComparer.Ordinal);

        foreach (IdentityRole role in roles)
        {
            providerRoleNames.Add(role.Name);

            RoleMetadata? existing = await roleMetadataStore
                .FindByNameAsync(role.Name, tenantId: null, clientId: clientId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var metadata = RoleMetadata.Create(
                    id: guidGenerator.Create(),
                    name: role.Name,
                    multiTenancySide: MultiTenancySides.Host,
                    tenantId: null,
                    clientId: clientId,
                    description: role.Description,
                    isSystem: false);

                await roleMetadataStore.AddAsync(metadata, cancellationToken).ConfigureAwait(false);
                added++;
            }
            else if (existing.IsOrphaned)
            {
                // Restore-on-return: the upstream role came back. Clear the orphan flag,
                // then apply any description drift in the same Update roundtrip.
                existing.RestoreFromOrphaned();
                if (!string.Equals(existing.Description, role.Description, StringComparison.Ordinal))
                {
                    existing.Rename(existing.Name, role.Description);
                }
                await roleMetadataStore.UpdateAsync(existing, cancellationToken: cancellationToken).ConfigureAwait(false);
                restored++;
            }
            else if (!string.Equals(existing.Description, role.Description, StringComparison.Ordinal))
            {
                existing.Rename(existing.Name, role.Description);
                await roleMetadataStore.UpdateAsync(existing, cancellationToken: cancellationToken).ConfigureAwait(false);
                updated++;
            }
            else
            {
                unchanged++;
            }
        }

        (int orphanedKept, int orphanedSoftDeleted, int orphanedHardDeleted) =
            await ApplyOrphanPolicyAsync(clientId, providerRoleNames, policy.OrphanedRolePolicy, cancellationToken)
                .ConfigureAwait(false);

        LogClientSynced(logger, policy.ProviderName, clientId,
            added, updated, unchanged, restored,
            orphanedKept, orphanedSoftDeleted, orphanedHardDeleted);
    }

    private async Task<(int Kept, int SoftDeleted, int HardDeleted)> ApplyOrphanPolicyAsync(
        string clientId,
        HashSet<string> providerRoleNames,
        OrphanedRolePolicy policy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleMetadata> known = await roleMetadataStore
            .ListByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);

        int kept = 0, softDeleted = 0, hardDeleted = 0;

        foreach (RoleMetadata row in known)
        {
            if (providerRoleNames.Contains(row.Name))
            {
                continue;
            }

            switch (policy)
            {
                case OrphanedRolePolicy.KeepAndLog:
                    LogOrphanKept(logger, row.Name, clientId);
                    kept++;
                    break;

                case OrphanedRolePolicy.SoftDelete:
                    if (!row.IsOrphaned)
                    {
                        row.MarkAsOrphaned(clock.Now);
                        await roleMetadataStore.UpdateAsync(row, cancellationToken: cancellationToken).ConfigureAwait(false);
                        LogOrphanSoftDeleted(logger, row.Name, clientId);
                        softDeleted++;
                    }
                    else
                    {
                        kept++;
                    }
                    break;

                case OrphanedRolePolicy.HardDelete:
                    await roleMetadataStore.RemoveAsync(row, cancellationToken).ConfigureAwait(false);
                    LogOrphanHardDeleted(logger, row.Name, clientId);
                    hardDeleted++;
                    break;
            }
        }

        return (kept, softDeleted, hardDeleted);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Provider} client-role sync disabled — skipping.")]
    private static partial void LogDisabled(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Provider} client-role sync enabled but no tracked client ids configured — skipping.")]
    private static partial void LogNoTrackedClients(ILogger logger, string provider);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{Provider} client '{ClientId}' not found upstream — skipping sync for this client.")]
    private static partial void LogClientNotFound(ILogger logger, string provider, string clientId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{Provider} admin API denied access for client '{ClientId}'. {Remediation}")]
    private static partial void LogForbidden(
        ILogger logger, Exception exception, string provider, string clientId, string remediation);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{Provider} client-role sync failed for client '{ClientId}'; continuing with the next tracked client.")]
    private static partial void LogClientSyncFailed(ILogger logger, Exception exception, string provider, string clientId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Provider} client-role sync for '{ClientId}' completed: " +
                  "{Added} added, {Updated} updated, {Unchanged} unchanged, {Restored} restored, " +
                  "{OrphanedKept} kept-orphaned, {OrphanedSoftDeleted} soft-deleted, {OrphanedHardDeleted} hard-deleted.")]
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Source-generated [LoggerMessage] partial — one parameter per template placeholder; collapsing into a wrapper would defeat structured logging.")]
    private static partial void LogClientSynced(
        ILogger logger, string provider, string clientId,
        int added, int updated, int unchanged, int restored,
        int orphanedKept, int orphanedSoftDeleted, int orphanedHardDeleted);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Orphaned role '{RoleName}' on client '{ClientId}' kept per policy — no state change.")]
    private static partial void LogOrphanKept(ILogger logger, string roleName, string clientId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Orphaned role '{RoleName}' on client '{ClientId}' marked as orphaned (soft delete).")]
    private static partial void LogOrphanSoftDeleted(ILogger logger, string roleName, string clientId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Orphaned role '{RoleName}' on client '{ClientId}' hard-deleted — referenced grants cascaded out.")]
    private static partial void LogOrphanHardDeleted(ILogger logger, string roleName, string clientId);
}
