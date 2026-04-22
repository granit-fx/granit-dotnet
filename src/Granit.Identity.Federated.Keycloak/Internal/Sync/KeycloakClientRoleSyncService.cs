using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Internal.Sync;

/// <summary>
/// Pure sync logic — enumerates Keycloak client-scope roles for every tracked client
/// and upserts matching <see cref="RoleMetadata"/> rows. Idempotent via the
/// <c>(Name, TenantId, ClientId)</c> unique index with <c>NULLS NOT DISTINCT</c>.
/// </summary>
/// <remarks>
/// Failure policy: log &amp; skip. Keycloak unreachable, service account missing
/// <c>realm-management</c> permissions, or a tracked client id not present in Keycloak
/// — each produces a log entry and moves on. The host never crashes on sync failure;
/// the next boot (or a future scheduled job) is a natural retry.
/// </remarks>
internal sealed partial class KeycloakClientRoleSyncService(
    IIdentityClientRoleManager clientRoleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
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
        Message = "Keycloak client-role sync for '{ClientId}' completed: {Added} added, {Updated} updated, {Unchanged} unchanged.")]
    private static partial void LogClientSynced(ILogger logger, string clientId, int added, int updated, int unchanged);
}
