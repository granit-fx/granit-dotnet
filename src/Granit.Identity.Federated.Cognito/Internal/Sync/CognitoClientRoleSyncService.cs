using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Granit.Identity.Federated.Cognito.Internal.Sync;

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
/// </remarks>
internal sealed partial class CognitoClientRoleSyncService(
    IIdentityClientRoleManager clientRoleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
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
            await SyncClientAsync(clientId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncClientAsync(string clientId, CancellationToken cancellationToken)
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

        foreach (IdentityRole role in roles)
        {
            RoleMetadata? existing = await roleMetadataStore
                .FindByNameAsync(role.Name, tenantId: null, clientId: clientId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var metadata = RoleMetadata.Create(
                    id: guidGenerator.Create(),
                    name: role.Name,
                    multiTenancySide: MultiTenancySide.Host,
                    tenantId: null,
                    clientId: clientId,
                    description: role.Description,
                    isSystem: false);

                await roleMetadataStore.AddAsync(metadata, cancellationToken).ConfigureAwait(false);
                added++;
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

        LogClientSynced(logger, clientId, added, updated, unchanged);
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
        Message = "Cognito client-role sync for '{ClientId}' completed: {Added} added, {Updated} updated, {Unchanged} unchanged.")]
    private static partial void LogClientSynced(ILogger logger, string clientId, int added, int updated, int unchanged);
}
