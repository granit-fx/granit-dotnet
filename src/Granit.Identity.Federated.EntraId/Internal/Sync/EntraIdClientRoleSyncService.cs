using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.EntraId.Internal.Sync;

/// <summary>
/// Pure sync logic — enumerates Entra App Roles for every tracked application and
/// upserts matching <see cref="RoleMetadata"/> rows. Idempotent via the
/// <c>(Name, TenantId, ClientId)</c> unique index. Failure policy: log &amp; skip.
/// </summary>
internal sealed partial class EntraIdClientRoleSyncService(
    IIdentityClientRoleManager clientRoleManager,
    IRoleMetadataStore roleMetadataStore,
    IGuidGenerator guidGenerator,
    IOptions<EntraIdClientRoleSyncOptions> options,
    ILogger<EntraIdClientRoleSyncService> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        EntraIdClientRoleSyncOptions opts = options.Value;

        if (!opts.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        if (opts.TrackedAppIds.Count == 0)
        {
            LogNoTrackedApps(logger);
            return;
        }

        foreach (string appId in opts.TrackedAppIds)
        {
            await SyncAppAsync(appId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncAppAsync(string appId, CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityRole> roles;
        try
        {
            roles = await clientRoleManager.GetClientRolesAsync(appId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (EntraIdClientNotFoundException)
        {
            LogAppNotFound(logger, appId);
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            LogForbidden(logger, ex, appId);
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LogAppSyncFailed(logger, ex, appId);
            return;
        }

        int added = 0;
        int updated = 0;
        int unchanged = 0;

        foreach (IdentityRole role in roles)
        {
            RoleMetadata? existing = await roleMetadataStore
                .FindByNameAsync(role.Name, tenantId: null, clientId: appId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var metadata = RoleMetadata.Create(
                    id: guidGenerator.Create(),
                    name: role.Name,
                    multiTenancySide: MultiTenancySide.Host,
                    tenantId: null,
                    clientId: appId,
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

        LogAppSynced(logger, appId, added, updated, unchanged);
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Entra ID client-role sync disabled — skipping.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Entra ID client-role sync enabled but no TrackedAppIds configured — skipping.")]
    private static partial void LogNoTrackedApps(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Entra ID: no Service Principal found for appId '{AppId}' — skipping sync for this app.")]
    private static partial void LogAppNotFound(ILogger logger, string appId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Microsoft Graph returned 403 for appId '{AppId}'. The service principal needs Application permissions: Application.Read.All (for listing), AppRoleAssignment.ReadWrite.All (for user assignments).")]
    private static partial void LogForbidden(ILogger logger, Exception exception, string appId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Entra ID client-role sync failed for appId '{AppId}'; continuing with the next tracked app.")]
    private static partial void LogAppSyncFailed(ILogger logger, Exception exception, string appId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Entra ID client-role sync for '{AppId}' completed: {Added} added, {Updated} updated, {Unchanged} unchanged.")]
    private static partial void LogAppSynced(ILogger logger, string appId, int added, int updated, int unchanged);
}
