using Granit.Authorization.Events;
using Granit.Authorization.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Cache;

/// <summary>
/// Wolverine message handler — invalidates the stale <see cref="IFusionCache"/>
/// entry when a permission grant is created or revoked.
/// </summary>
/// <remarks>
/// <para>
/// Discovered by Wolverine's handler scanning convention: class name ends in
/// <c>"Handler"</c>, method named <c>HandleAsync</c> with the message as first parameter.
/// Additional parameters are resolved from the application's DI container.
/// No WolverineFx package reference is required in this project.
/// </para>
/// <para>
/// VULN-204 fix: revocations use <see cref="IFusionCache.RemoveAsync"/> (hard delete)
/// to prevent stale-while-revalidate from serving a revoked grant. Grants use
/// <see cref="IFusionCache.ExpireAsync"/> (soft expire) for resilience — a stale
/// "denied" entry is safe if the factory fails.
/// </para>
/// </remarks>
public static class PermissionCacheInvalidationHandler
{
    /// <summary>
    /// Invalidates the cached permission grant entry for the changed role and tenant scope.
    /// </summary>
    public static async Task HandleAsync(
        PermissionGrantChangedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken)
    {
        string key = PermissionChecker.BuildCacheKey(
            @event.TenantId, @event.RoleName, @event.PermissionName);

        if (@event.IsGranted)
        {
            // Grant created: soft-expire the old "denied" entry.
            // Stale "denied" is safe — factory will refresh to "granted".
            await cache.ExpireAsync(key, token: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Grant revoked: hard-remove to prevent stale-while-revalidate
            // from serving the old "granted" entry.
            await cache.RemoveAsync(key, token: cancellationToken).ConfigureAwait(false);
        }
    }
}
