using System.Diagnostics.CodeAnalysis;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Granit.Identity.Federated.Cognito.Sync;

/// <summary>
/// Pure sync logic — enumerates Cognito groups matching the
/// <c>{appClientId}{Delimiter}*</c> naming convention for every tracked app-client id
/// and upserts matching <see cref="RoleMetadata"/> rows. Idempotent via the
/// <c>(Name, TenantId, ClientId)</c> unique index with <c>NULLS NOT DISTINCT</c>.
/// </summary>
/// <remarks>
/// <para>
/// Un-prefixed Cognito groups keep flowing through the existing realm-role path
/// (<c>IIdentityRoleManager.GetRolesAsync</c> + role orchestrator) and surface as
/// <c>ClientId = null</c>. This sync only processes groups whose name starts with a
/// tracked-client prefix — no double-write.
/// </para>
/// <para>
/// Failure policy: log &amp; skip. AWS unreachable, missing IAM permission, or an
/// un-tracked client id in the configuration — each produces a log entry and moves on.
/// The host never crashes on sync failure; the next boot is a natural retry. See
/// ADR-027.
/// </para>
/// <para>
/// Orphan handling (ADR-029): after the upsert pass, rows in the store that are no
/// longer returned by the upstream provider are processed per
/// <see cref="CognitoClientRoleSyncOptions.OrphanedRolePolicy"/>. Restore-on-return
/// is automatic — if a previously orphaned row starts being returned again the flag
/// is cleared.
/// </para>
/// </remarks>
public sealed partial class CognitoClientRoleSyncService(
    IIdentityClientRoleManager clientRoleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
    IClock clock,
    IOptions<CognitoClientRoleSyncOptions> options,
    ILogger<CognitoClientRoleSyncService> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        CognitoClientRoleSyncOptions opts = options.Value;

        if (!opts.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        if (opts.TrackedAppClientIds.Count == 0)
        {
            LogNoTrackedClients(logger);
            return;
        }

        foreach (string clientId in opts.TrackedAppClientIds)
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
        catch (NotAuthorizedException ex)
        {
            LogForbidden(logger, ex, clientId);
            return;
        }
        catch (AmazonCognitoIdentityProviderException ex)
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
                existing.RestoreFromOrphaned();
                if (!string.Equals(existing.Description, role.Description, StringComparison.Ordinal))
                {
                    existing.Rename(existing.Name, role.Description);
                }
                await roleMetadataStore.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
                restored++;
            }
            else if (!string.Equals(existing.Description, role.Description, StringComparison.Ordinal))
            {
                existing.Rename(existing.Name, role.Description);
                await roleMetadataStore.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
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
                        await roleMetadataStore.UpdateAsync(row, cancellationToken).ConfigureAwait(false);
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
        Message = "Cognito client-role sync disabled — skipping.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cognito client-role sync enabled but no TrackedAppClientIds configured — skipping.")]
    private static partial void LogNoTrackedClients(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "AWS Cognito returned NotAuthorized for client '{ClientId}'. The admin IAM principal needs: cognito-idp:ListGroups, cognito-idp:AdminListGroupsForUser, cognito-idp:ListUserPoolClients on the User Pool.")]
    private static partial void LogForbidden(ILogger logger, Exception exception, string clientId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cognito client-role sync failed for client '{ClientId}'; continuing with the next tracked client.")]
    private static partial void LogClientSyncFailed(ILogger logger, Exception exception, string clientId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cognito client-role sync for '{ClientId}' completed: " +
                  "{Added} added, {Updated} updated, {Unchanged} unchanged, {Restored} restored, " +
                  "{OrphanedKept} kept-orphaned, {OrphanedSoftDeleted} soft-deleted, {OrphanedHardDeleted} hard-deleted.")]
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Source-generated [LoggerMessage] partial — one parameter per template placeholder; collapsing into a wrapper would defeat structured logging.")]
    private static partial void LogClientSynced(
        ILogger logger, string clientId,
        int added, int updated, int unchanged, int restored,
        int orphanedKept, int orphanedSoftDeleted, int orphanedHardDeleted);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cognito orphaned role '{RoleName}' on clientId '{ClientId}' kept per policy — no state change.")]
    private static partial void LogOrphanKept(ILogger logger, string roleName, string clientId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cognito orphaned role '{RoleName}' on clientId '{ClientId}' marked as orphaned (soft delete).")]
    private static partial void LogOrphanSoftDeleted(ILogger logger, string roleName, string clientId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cognito orphaned role '{RoleName}' on clientId '{ClientId}' hard-deleted — referenced grants cascaded out.")]
    private static partial void LogOrphanHardDeleted(ILogger logger, string roleName, string clientId);
}
