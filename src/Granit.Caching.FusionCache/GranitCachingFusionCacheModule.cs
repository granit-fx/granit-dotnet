using Granit.Caching.FusionCache.Extensions;
using Granit.Caching.StackExchangeRedis;
using Granit.Core.Modularity;
using Granit.Timing;

namespace Granit.Caching.FusionCache;

/// <summary>
/// Granit module for the FusionCache provider (L1+L2+backplane, Kubernetes-ready).
/// </summary>
/// <remarks>
/// <para>
/// Combines a per-pod L1 in-memory cache with a shared L2 Redis cache
/// and a Redis backplane for real-time cross-pod L1 invalidation.
/// </para>
/// <para>
/// Production features: fail-safe (stale on error), factory timeouts (soft + hard),
/// eager refresh (background refresh before expiration), OpenTelemetry metrics and traces.
/// </para>
/// <para>
/// <c>appsettings.json</c> configuration:
/// <code>
/// {
///   "Cache": {
///     "KeyPrefix": "myapp",
///     "EncryptValues": true,
///     "Encryption": { "Key": "base64-key-from-vault" },
///     "Redis": { "Configuration": "redis:6379", "InstanceName": "myapp:" },
///     "FusionCache": {
///       "FailSafeIsEnabled": true,
///       "FailSafeMaxDuration": "02:00:00",
///       "FactorySoftTimeout": "00:00:02",
///       "EagerRefreshThreshold": 0.8
///     }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingRedisModule))]
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitCachingFusionCacheModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCachingFusionCache();
}
