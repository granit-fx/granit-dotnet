using Granit.Authorization.Events;
using Granit.Authorization.Services;
using Granit.Caching;

namespace Granit.Authorization.Cache;

/// <summary>
/// Wolverine message handler — removes the stale <see cref="ICacheService{PermissionGrantCacheItem}"/>
/// entry when a permission grant is created or revoked.
/// </summary>
/// <remarks>
/// Discovered by Wolverine's handler scanning convention: class name ends in
/// <c>"Handler"</c>, method named <c>HandleAsync</c> with the message as first parameter.
/// Additional parameters are resolved from the application's DI container.
/// No WolverineFx package reference is required in this project.
/// </remarks>
public static class PermissionCacheInvalidationHandler
{
    /// <summary>
    /// Removes the cached permission grant entry for the changed role and tenant scope.
    /// </summary>
    public static async Task HandleAsync(
        PermissionGrantChangedEvent @event,
        ICacheService<PermissionGrantCacheItem> cache,
        CancellationToken cancellationToken)
    {
        string key = PermissionChecker.BuildCacheKey(
            @event.TenantId, @event.RoleName, @event.PermissionName);
        await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
    }
}
