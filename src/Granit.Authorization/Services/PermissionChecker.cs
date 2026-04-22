using Granit.Authorization.Cache;
using Granit.Authorization.Diagnostics;
using Granit.Authorization.Options;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Services;

/// <summary>
/// Scoped permission checker implementing the full verification pipeline:
/// <list type="number">
/// <item>Not authenticated → denied</item>
/// <item>AlwaysAllow (dev/test, authenticated users only) → granted</item>
/// <item>AdminRole bypass (root of trust, case-insensitive) → granted without DB</item>
/// <item>Permission undefined → <see cref="InvalidOperationException"/></item>
/// <item>Permission's <see cref="MultiTenancySide"/> incompatible with current tenant context → denied</item>
/// <item>For each registered <see cref="IPermissionGrantProvider"/> (default order: User, Role, Client),
///   query each of the provider's keys through cache / store. First positive match wins (fail-fast).</item>
/// </list>
/// Cache key format: <c>perm:{tenantId|"global"}:{providerName}:{providerKey}:{permissionName}</c>
/// </summary>
internal sealed class PermissionChecker(
    ICurrentUserService currentUserService,
    ICurrentTenant currentTenant,
    IPermissionDefinitionManager definitionManager,
    IPermissionGrantStore grantStore,
    IEnumerable<IPermissionGrantProvider> grantProviders,
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

        PermissionDefinition? definition = definitionManager.Find(permissionName);
        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Permission '{permissionName}' is not defined. Register it via IPermissionDefinitionProvider.");
        }

        // Explicit IsAvailable check per soft-dependency contract (NullTenantContext returns null).
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string tenantIdStr = tenantId?.ToString() ?? "global";

        if (!IsCompatibleWithCurrentSide(definition, currentTenant))
        {
            metrics.RecordCheckDenied(tenantIdStr);
            return false;
        }

        PermissionGrantLookupContext lookup = BuildLookupContext(currentUserService, roles);

        foreach (IPermissionGrantProvider provider in grantProviders)
        {
            IReadOnlyList<string> providerKeys = provider.GetProviderKeys(lookup);
            foreach (string providerKey in providerKeys)
            {
                PermissionGrantCacheItem result = await cache.GetOrSetAsync<PermissionGrantCacheItem>(
                    BuildCacheKey(tenantId, provider.Name, providerKey, permissionName),
                    async (_, ct) =>
                    {
                        metrics.RecordCacheMiss(tenantIdStr);
                        return new PermissionGrantCacheItem(
                            await grantStore.IsGrantedAsync(
                                provider.Name, providerKey, permissionName, tenantId, ct)
                            .ConfigureAwait(false));
                    },
                    options: new FusionCacheEntryOptions { Duration = opts.CacheDuration },
                    tags: BuildCacheTags(provider.Name, providerKey),
                    token: cancellationToken).ConfigureAwait(false);

                if (result.IsGranted)
                {
                    return true;
                }
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
        PermissionGrantLookupContext lookup = BuildLookupContext(currentUserService, roles);

        (HashSet<string> granted, List<string> uncached) = await PartitionCachedPermissionsAsync(
            permissionNames, lookup, tenantId, cancellationToken).ConfigureAwait(false);

        if (uncached.Count == 0)
        {
            return [.. granted];
        }

        await QueryAndCachePermissionsAsync(uncached, lookup, tenantId, granted, opts, cancellationToken)
            .ConfigureAwait(false);

        return [.. granted];
    }

    // Walks each (permission, provider, key) triplet and splits them into cached-grants
    // vs. uncached. A permission is added to 'uncached' at most once, even if several
    // provider keys miss the cache.
    private async Task<(HashSet<string> Granted, List<string> Uncached)> PartitionCachedPermissionsAsync(
        IReadOnlyList<string> permissionNames,
        PermissionGrantLookupContext lookup,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        HashSet<string> granted = new(StringComparer.Ordinal);
        List<string> uncached = [];

        foreach (string permissionName in permissionNames)
        {
            PermissionDefinition? definition = definitionManager.Find(permissionName);
            if (definition is null)
            {
                continue;
            }

            if (!IsCompatibleWithCurrentSide(definition, currentTenant))
            {
                continue;
            }

            await PartitionPermissionAsync(permissionName, lookup, tenantId, granted, uncached, cancellationToken)
                .ConfigureAwait(false);
        }

        return (granted, uncached);
    }

    private async Task PartitionPermissionAsync(
        string permissionName,
        PermissionGrantLookupContext lookup,
        Guid? tenantId,
        HashSet<string> granted,
        List<string> uncached,
        CancellationToken cancellationToken)
    {
        bool uncachedRecorded = false;

        foreach (IPermissionGrantProvider provider in grantProviders)
        {
            foreach (string providerKey in provider.GetProviderKeys(lookup))
            {
                MaybeValue<PermissionGrantCacheItem> cached = await cache.TryGetAsync<PermissionGrantCacheItem>(
                    BuildCacheKey(tenantId, provider.Name, providerKey, permissionName),
                    token: cancellationToken).ConfigureAwait(false);

                if (cached.HasValue)
                {
                    if (cached.Value.IsGranted)
                    {
                        granted.Add(permissionName);
                        uncachedRecorded = true; // cache is authoritative for this pair
                    }
                }
                else if (!uncachedRecorded)
                {
                    uncached.Add(permissionName);
                    uncachedRecorded = true;
                }
            }
        }
    }

    // Batch-query the store for all uncached permissions per (provider, key), then populate the cache.
    private async Task QueryAndCachePermissionsAsync(
        List<string> uncached,
        PermissionGrantLookupContext lookup,
        Guid? tenantId,
        HashSet<string> granted,
        GranitAuthorizationOptions opts,
        CancellationToken cancellationToken)
    {
        FusionCacheEntryOptions entryOptions = new() { Duration = opts.CacheDuration };

        foreach (IPermissionGrantProvider provider in grantProviders)
        {
            foreach (string providerKey in provider.GetProviderKeys(lookup))
            {
                IReadOnlyList<string> granteeGrants = await grantStore.GetGrantedAsync(
                    provider.Name, providerKey, uncached, tenantId, cancellationToken).ConfigureAwait(false);

                foreach (string perm in granteeGrants)
                {
                    granted.Add(perm);
                }

                IEnumerable<string>? tags = BuildCacheTags(provider.Name, providerKey);
                foreach (string perm in uncached)
                {
                    bool isGranted = granteeGrants.Contains(perm);
                    await cache.SetAsync(
                        BuildCacheKey(tenantId, provider.Name, providerKey, perm),
                        new PermissionGrantCacheItem(isGranted),
                        options: entryOptions,
                        tags: tags,
                        token: cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Builds the cache key for a permission check. Scopes the cache by tenant, provider and
    /// provider key so that grants to a user and grants to a role of the same textual key
    /// (unlikely but possible) never collide.
    /// </summary>
    internal static string BuildCacheKey(Guid? tenantId, string providerName, string providerKey, string permissionName) =>
        $"perm:{tenantId?.ToString() ?? "global"}:{providerName}:{providerKey}:{permissionName}";

    /// <summary>
    /// Builds the FusionCache tag set for a permission grant cache entry. Role-scope
    /// grants are tagged with <c>role:{roleName}</c> so <c>RoleUpdatedEvent</c> /
    /// <c>RoleDeletedEvent</c> handlers can flush every stale entry for a given role
    /// via <c>RemoveByTagAsync</c> in one shot — across every tenant that had cached
    /// it. User and client grants currently have no tag (cache invalidation for those
    /// flows goes through <see cref="Cache.PermissionCacheInvalidationHandler"/>
    /// listening on <c>PermissionGrantChangedEvent</c>).
    /// </summary>
    internal static IEnumerable<string>? BuildCacheTags(string providerName, string providerKey) =>
        providerName == PermissionGrantProviderNames.Role
            ? [RoleTag(providerKey)]
            : null;

    /// <summary>Tag applied to every role-scope grant cache entry for <paramref name="roleName"/>.</summary>
    internal static string RoleTag(string roleName) => $"role:{roleName}";

    // Side enforcement: a Host-sided permission is only grantable when no tenant is active;
    // a Tenant-sided one only when a tenant is active. Both-sided permissions pass in any context.
    // When Granit.MultiTenancy is absent, NullTenantContext.IsAvailable is always false — so
    // Tenant-sided permissions are uniformly denied, which is consistent: a consumer without
    // multi-tenancy should not declare Tenant-sided permissions in the first place.
    internal static bool IsCompatibleWithCurrentSide(PermissionDefinition definition, ICurrentTenant currentTenant) =>
        currentTenant.IsAvailable
            ? definition.MultiTenancySide.HasFlag(MultiTenancySide.Tenant)
            : definition.MultiTenancySide.HasFlag(MultiTenancySide.Host);

    private static PermissionGrantLookupContext BuildLookupContext(
        ICurrentUserService currentUserService,
        IReadOnlyList<string> roles) =>
        new(currentUserService.UserId, roles, currentUserService.ClientId);
}
