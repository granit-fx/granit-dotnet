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
                async (_, ct) =>
                {
                    metrics.RecordCacheMiss(tenantIdStr);
                    return new PermissionGrantCacheItem(
                        await grantStore.IsGrantedAsync(role, permissionName, tenantId, ct).ConfigureAwait(false));
                },
                new FusionCacheEntryOptions { Duration = opts.CacheDuration },
                token: cancellationToken).ConfigureAwait(false);

            if (result.IsGranted)
            {
                return true;
            }
        }

        metrics.RecordCheckDenied(tenantIdStr);
        return false;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGrantedAsync(
        IReadOnlyList<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        GranitAuthorizationOptions opts = options.Value;

        if (!currentUserService.IsAuthenticated)
        {
            return [];
        }

        if (opts.AlwaysAllow)
        {
            return permissionNames;
        }

        IReadOnlyList<string> roles = currentUserService.GetRoles();

        if (opts.AdminRoles.Any(adminRole => roles.Any(
            r => string.Equals(r, adminRole, StringComparison.OrdinalIgnoreCase))))
        {
            return permissionNames;
        }

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        // Partition into cached hits and uncached misses
        HashSet<string> granted = new(StringComparer.Ordinal);
        List<string> uncached = [];

        foreach (string permissionName in permissionNames)
        {
            if (!definitionManager.Exists(permissionName))
            {
                continue;
            }

            bool found = false;
            foreach (string role in roles)
            {
                MaybeValue<PermissionGrantCacheItem> cached = await cache.TryGetAsync<PermissionGrantCacheItem>(
                    BuildCacheKey(tenantId, role, permissionName),
                    token: cancellationToken).ConfigureAwait(false);

                if (cached.HasValue)
                {
                    if (cached.Value.IsGranted)
                    {
                        granted.Add(permissionName);
                        found = true;
                    }
                }
                else if (!found)
                {
                    uncached.Add(permissionName);
                    found = true; // only add once to uncached list
                }
            }
        }

        if (uncached.Count == 0)
        {
            return [.. granted];
        }

        // Batch query the store for all uncached permissions per role
        foreach (string role in roles)
        {
            IReadOnlyList<string> roleGrants = await grantStore.GetGrantedAsync(
                role, uncached, tenantId, cancellationToken).ConfigureAwait(false);

            foreach (string perm in roleGrants)
            {
                granted.Add(perm);
            }

            // Populate cache for all queried permissions in this role
            foreach (string perm in uncached)
            {
                bool isGranted = roleGrants.Contains(perm);
                await cache.SetAsync(
                    BuildCacheKey(tenantId, role, perm),
                    new PermissionGrantCacheItem(isGranted),
                    new FusionCacheEntryOptions { Duration = opts.CacheDuration },
                    token: cancellationToken).ConfigureAwait(false);
            }
        }

        return [.. granted];
    }

    internal static string BuildCacheKey(Guid? tenantId, string roleName, string permissionName) =>
        $"perm:{tenantId?.ToString() ?? "global"}:{roleName}:{permissionName}";
}
