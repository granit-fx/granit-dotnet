using System.Diagnostics.CodeAnalysis;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Sync;

/// <summary>
/// Pure sync logic — enumerates Keycloak client-scope roles for every tracked client
/// and upserts matching <see cref="RoleMetadata"/> rows. Idempotent via the
/// <c>(Name, TenantId, ClientId)</c> unique index with <c>NULLS NOT DISTINCT</c>.
/// </summary>
/// <remarks>
/// <para>
/// Failure policy: log &amp; skip. Keycloak unreachable, service account missing
/// <c>realm-management</c> permissions, or a tracked client id not present in Keycloak
/// — each produces a log entry and moves on. The host never crashes on sync failure;
/// the next boot (or a future scheduled job) is a natural retry.
/// </para>
/// <para>
/// Orphan handling (ADR-029): after the upsert pass, rows in the store that are no
/// longer returned by the upstream provider are processed per
/// <see cref="KeycloakClientRoleSyncOptions.OrphanedRolePolicy"/>. Restore-on-return
/// is automatic — if a previously orphaned row starts being returned again the flag
/// is cleared.
/// </para>
/// </remarks>
public sealed partial class KeycloakClientRoleSyncService(
    IIdentityClientRoleManager clientRoleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
    IClock clock,
    IOptions<KeycloakClientRoleSyncOptions> options,
    ILogger<KeycloakClientRoleSyncService> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        KeycloakClientRoleSyncOptions opts = options.Value;

        if (!opts.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        if (opts.TrackedClientIds.Count == 0)
        {
            LogNoTrackedClients(logger);
            return;
        }

        foreach (string clientId in opts.TrackedClientIds)
        {
            await SyncClientAsync(clientId, opts.OrphanedRolePolicy, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncClientAsync(
        string clientId, OrphanedRolePolicy orphanPolicy, CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityRole> roles;
        try
        {
            roles = await clientRoleManager.GetClientRolesAsync(clientId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeycloakClientNotFoundException)
        {
            LogClientNotFound(logger, clientId);
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            LogForbidden(logger, ex, clientId);
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogClientSyncFailed(logger, ex, clientId);
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
            await ApplyOrphanPolicyAsync(clientId, providerRoleNames, orphanPolicy, cancellationToken)
                .ConfigureAwait(false);

        LogClientSynced(logger, clientId,
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

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Keycloak client-role sync disabled — skipping.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak client-role sync enabled but no TrackedClientIds configured — skipping.")]
    private static partial void LogNoTrackedClients(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak client '{ClientId}' not found in the configured realm — skipping sync for this client.")]
    private static partial void LogClientNotFound(ILogger logger, string clientId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Keycloak admin API returned 403 for client '{ClientId}'. The admin service account needs realm-management roles: view-clients, query-clients, view-realm.")]
    private static partial void LogForbidden(ILogger logger, Exception exception, string clientId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak client-role sync failed for client '{ClientId}'; continuing with the next tracked client.")]
    private static partial void LogClientSyncFailed(ILogger logger, Exception exception, string clientId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak client-role sync for '{ClientId}' completed: " +
                  "{Added} added, {Updated} updated, {Unchanged} unchanged, {Restored} restored, " +
                  "{OrphanedKept} kept-orphaned, {OrphanedSoftDeleted} soft-deleted, {OrphanedHardDeleted} hard-deleted.")]
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Source-generated [LoggerMessage] partial — one parameter per template placeholder; collapsing into a wrapper would defeat structured logging.")]
    private static partial void LogClientSynced(
        ILogger logger, string clientId,
        int added, int updated, int unchanged, int restored,
        int orphanedKept, int orphanedSoftDeleted, int orphanedHardDeleted);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak orphaned role '{RoleName}' on client '{ClientId}' kept per policy — no state change.")]
    private static partial void LogOrphanKept(ILogger logger, string roleName, string clientId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Keycloak orphaned role '{RoleName}' on client '{ClientId}' marked as orphaned (soft delete).")]
    private static partial void LogOrphanSoftDeleted(ILogger logger, string roleName, string clientId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Keycloak orphaned role '{RoleName}' on client '{ClientId}' hard-deleted — referenced grants cascaded out.")]
    private static partial void LogOrphanHardDeleted(ILogger logger, string roleName, string clientId);
}
