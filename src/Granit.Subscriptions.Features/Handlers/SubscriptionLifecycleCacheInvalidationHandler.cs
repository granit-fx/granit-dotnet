using Granit.Features.Cache;
using Granit.Features.Definitions;
using Granit.Subscriptions.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Subscriptions.Features.Handlers;

/// <summary>
/// Wolverine message handler — expires all tenant feature cache entries when a subscription
/// is suspended, cancelled, or expired, preventing stale premium access after payment failure.
/// </summary>
/// <remarks>
/// Discovered by Wolverine's handler scanning convention: <c>public class</c> with
/// <c>public static HandleAsync</c> methods. The implicit parameterless constructor is
/// intentional — do not add constructors.
/// </remarks>
public class SubscriptionLifecycleCacheInvalidationHandler
{
    /// <summary>Expires all cached feature values for the suspended subscription's tenant.</summary>
    public static async Task HandleAsync(
        SubscriptionSuspendedEto eto,
        IFusionCache cache,
        IFeatureDefinitionStore definitionStore,
        CancellationToken cancellationToken) =>
        await InvalidateTenantFeaturesAsync(eto.TenantId, cache, definitionStore, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Expires all cached feature values for the cancelled subscription's tenant.</summary>
    public static async Task HandleAsync(
        SubscriptionCancelledEto eto,
        IFusionCache cache,
        IFeatureDefinitionStore definitionStore,
        CancellationToken cancellationToken) =>
        await InvalidateTenantFeaturesAsync(eto.TenantId, cache, definitionStore, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Expires all cached feature values for the expired subscription's tenant.</summary>
    public static async Task HandleAsync(
        SubscriptionExpiredEto eto,
        IFusionCache cache,
        IFeatureDefinitionStore definitionStore,
        CancellationToken cancellationToken) =>
        await InvalidateTenantFeaturesAsync(eto.TenantId, cache, definitionStore, cancellationToken)
            .ConfigureAwait(false);

    private static async Task InvalidateTenantFeaturesAsync(
        Guid tenantId,
        IFusionCache cache,
        IFeatureDefinitionStore definitionStore,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FeatureDefinition> definitions = definitionStore.GetAll();
        foreach (FeatureDefinition definition in definitions)
        {
            string key = FeatureCacheKey.Build(tenantId, definition.Name);
            await cache.ExpireAsync(key, token: cancellationToken).ConfigureAwait(false);
        }
    }
}
