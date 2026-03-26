using Granit.Authorization.Abstractions;
using Granit.Authorization.Cache;
using Granit.Authorization.Diagnostics;
using Granit.Authorization.Options;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Services;

/// <summary>
/// Scoped permission checker implementing the full RBAC verification pipeline:
/// <list type="number">
/// <item>Not authenticated → denied</item>
/// <item>AlwaysAllow (dev/test, authenticated users only) → granted</item>
/// <item>AdminRole bypass (root of trust, case-insensitive) → granted without DB</item>
/// <item>Permission undefined → <see cref="InvalidOperationException"/></item>
/// <item>For each role: cache hit or store query; any true → granted</item>
/// </list>
/// Cache key format: <c>perm:{tenantId|"global"}:{roleName}:{permissionName}</c>
/// </summary>
internal sealed class PermissionChecker(
    ICurrentUserService currentUserService,
    ICurrentTenant currentTenant,
    IPermissionDefinitionManager definitionManager,
    IPermissionGrantStore grantStore,
    IFusionCache cache,
    AuthorizationMetrics metrics,
    IOptions<GranitAuthorizationOptions> options) : IPermissionChecker
{
    /// <inheritdoc />
    public async Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        GranitAuthorizationOptions opts = options.Value;

        // VULN-001 fix: authentication MUST be checked before AlwaysAllow to prevent
        // a configuration error from granting access to anonymous users.
        if (!currentUserService.IsAuthenticated)
        {
            return false;
        }

        if (opts.AlwaysAllow)
        {
            return true;
        }

        IReadOnlyList<string> roles = currentUserService.GetRoles();

        // VULN-301 fix: case-insensitive comparison prevents mismatch with IdP role casing.
        if (opts.AdminRoles.Any(adminRole => roles.Any(
            r => string.Equals(r, adminRole, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        if (!definitionManager.Exists(permissionName))
        {
            throw new InvalidOperationException(
                $"Permission '{permissionName}' is not defined. Register it via IPermissionDefinitionProvider.");
        }

        // Explicit IsAvailable check per soft-dependency contract (NullTenantContext returns null).
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string tenantIdStr = tenantId?.ToString() ?? "global";

        foreach (string role in roles)
        {
            PermissionGrantCacheItem result = await cache.GetOrSetAsync<PermissionGrantCacheItem>(
                BuildCacheKey(tenantId, role, permissionName),
                async (_, ct) => new PermissionGrantCacheItem
                {
                    IsGranted = await grantStore.IsGrantedAsync(role, permissionName, tenantId, ct).ConfigureAwait(false)
                },
                new FusionCacheEntryOptions { Duration = opts.CacheDuration },
                token: cancellationToken).ConfigureAwait(false);

            if (result.IsGranted)
            {
                metrics.RecordCheckGranted(tenantIdStr);
                return true;
            }
        }

        metrics.RecordCheckDenied(tenantIdStr);
        return false;
    }

    internal static string BuildCacheKey(Guid? tenantId, string roleName, string permissionName) =>
        $"perm:{tenantId?.ToString() ?? "global"}:{roleName}:{permissionName}";
}
