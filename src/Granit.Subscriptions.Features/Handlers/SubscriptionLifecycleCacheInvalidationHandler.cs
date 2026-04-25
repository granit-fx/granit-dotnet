using System.Diagnostics.CodeAnalysis;
using Granit.Features.Cache;
using Granit.Features.Definitions;
using Granit.Subscriptions.Events;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Subscriptions.Features.Handlers;

/// <summary>
/// Wolverine message handler — expires all per-tenant feature cache entries when a
/// subscription is suspended, cancelled, or expired.
/// </summary>
/// <remarks>
/// Feature values are cached per (tenantId, featureName). When the subscription
/// lifecycle changes, the resolved values may differ (e.g., downgraded plan), so
/// stale entries must be evicted immediately. Discovered by Wolverine's handler
/// scanning convention: non-static public class with public static HandleAsync methods.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class SubscriptionLifecycleCacheInvalidationHandler
{
    /// <summary>Invalidates the feature cache when a subscription is suspended.</summary>
    public static async Task HandleAsync(
        SubscriptionSuspendedEto eto,
        IFeatureDefinitionStore definitionStore,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        await InvalidateForTenantAsync(eto.TenantId, definitionStore, cache, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Invalidates the feature cache when a subscription is cancelled.</summary>
    public static async Task HandleAsync(
        SubscriptionCancelledEto eto,
        IFeatureDefinitionStore definitionStore,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        await InvalidateForTenantAsync(eto.TenantId, definitionStore, cache, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Invalidates the feature cache when a subscription expires.</summary>
    public static async Task HandleAsync(
        SubscriptionExpiredEto eto,
        IFeatureDefinitionStore definitionStore,
        IFusionCache cache,
        CancellationToken cancellationToken) =>
        await InvalidateForTenantAsync(eto.TenantId, definitionStore, cache, cancellationToken)
            .ConfigureAwait(false);

    private static async Task InvalidateForTenantAsync(
        Guid tenantId,
        IFeatureDefinitionStore definitionStore,
        IFusionCache cache,
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
