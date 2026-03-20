using Granit.Features.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Features.Cache;

/// <summary>
/// Wolverine message handler — expires the stale <see cref="IFusionCache"/> entry
/// when a feature value override is created, updated, or deleted.
/// </summary>
/// <remarks>
/// Discovered by Wolverine's handler scanning convention: class name ends in
/// <c>"Handler"</c>, method named <c>HandleAsync</c> with the message as first parameter.
/// Additional parameters are resolved from the application's DI container.
/// No WolverineFx package reference is required in this project.
/// </remarks>
public static class FeatureCacheInvalidationHandler
{
    /// <summary>
    /// Expires the cached resolved-value entry for the changed feature and tenant scope.
    /// </summary>
    public static async Task HandleAsync(
        FeatureValueChangedEvent @event,
        IFusionCache cache,
        CancellationToken cancellationToken)
    {
        string key = FeatureCacheKey.Build(@event.TenantId, @event.FeatureName);
        await cache.ExpireAsync(key, token: cancellationToken).ConfigureAwait(false);
    }
}
