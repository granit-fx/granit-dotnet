using Granit.Authorization.Events;
using Granit.Authorization.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Cache;

/// <summary>
/// Wolverine message handlers — flush stale permission-check cache entries when a
/// role is renamed or deleted.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PermissionChecker"/> tags every role-scope grant cache entry with
/// <c>role:{roleName}</c>. A rename or delete therefore only needs to call
/// <see cref="IFusionCache.RemoveByTagAsync(string,FusionCacheEntryOptions?,CancellationToken)"/>
/// on the stale name; every tenant that had cached a check for that role is
/// flushed in a single call.
/// </para>
/// <para>
/// Discovered by Wolverine's handler scanning convention: class name ends in
/// <c>"Handler"</c>, method named <c>HandleAsync</c> with the message as first
/// parameter. Additional parameters are resolved from the application's DI
/// container. No WolverineFx package reference is required in this project.
/// </para>
/// </remarks>
public class RoleCacheInvalidationHandler
{
    /// <summary>
    /// Invalidates role-scope permission cache entries for the renamed role. When
    /// <see cref="RoleUpdatedEvent.PreviousName"/> is <see langword="null"/> (description-only
    /// update) the cache is left untouched because the role-name-based tag is stable.
    /// </summary>
    public static async Task HandleAsync(
        RoleUpdatedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken)
    {
        if (@event.PreviousName is null)
        {
            return;
        }

        await cache.RemoveByTagAsync(
            PermissionChecker.RoleTag(@event.PreviousName),
            token: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidates every role-scope permission cache entry for the deleted role
    /// across every tenant. Subsequent permission checks for that name re-query the
    /// grant store, which has no rows for the role, and return <c>denied</c>.
    /// </summary>
    public static Task HandleAsync(
        RoleDeletedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        cache.RemoveByTagAsync(
            PermissionChecker.RoleTag(@event.Name),
            token: cancellationToken).AsTask();
}
