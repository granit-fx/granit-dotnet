using Granit.Authorization.Events;
using Granit.Authorization.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Cache;

/// <summary>
/// Wolverine message handler — expires the stale <see cref="IFusionCache"/>
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
    /// Expires the cached permission grant entry for the changed role and tenant scope.
    /// </summary>
    public static async Task HandleAsync(
        PermissionGrantChangedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken)
    {
        string key = PermissionChecker.BuildCacheKey(
            @event.TenantId, @event.RoleName, @event.PermissionName);
        await cache.ExpireAsync(key, token: cancellationToken).ConfigureAwait(false);
    }
}
