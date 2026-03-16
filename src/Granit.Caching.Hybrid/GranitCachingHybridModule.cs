using Granit.Caching.Hybrid.Extensions;
using Granit.Caching.StackExchangeRedis;
using Granit.Core.Modularity;
using Granit.Timing;

namespace Granit.Caching.Hybrid;

/// <summary>
/// Granit module for the HybridCache provider (L1+L2, Kubernetes).
/// Combines a per-pod L1 in-memory cache and a shared L2 Redis cache.
/// </summary>
/// <remarks>
/// This module depends on <c>GranitCachingRedisModule</c> (which itself depends on
/// <c>GranitCachingModule</c>). Initialization order is guaranteed by
/// the Granit module system.
/// <para>
/// Cross-pod invalidation behaviour:
/// <see cref="Microsoft.Extensions.Caching.Hybrid.HybridCache"/> does not invalidate remote L1 caches.
/// Set <c>Cache:Hybrid:LocalCacheExpiration</c> to ≤ 60 s (default: 30 s)
/// to bound the stale-data window between pods.
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
///     "Hybrid": { "LocalCacheExpiration": "00:00:30" }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingRedisModule))]
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitCachingHybridModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCachingHybrid();
}
