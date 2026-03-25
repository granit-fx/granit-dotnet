using Granit.Caching.Extensions;
using Granit.Modularity;
using Granit.Timing;

namespace Granit.Caching;

/// <summary>
/// Granit module for FusionCache-based caching with L1 in-memory, fail-safe, and OpenTelemetry.
/// </summary>
/// <remarks>
/// Registers <see cref="ZiggyCreatures.Caching.Fusion.IFusionCache"/> with an L1 in-memory cache,
/// <see cref="Options.CachingOptions"/>, <see cref="Options.FusionCachingOptions"/>,
/// <see cref="Options.CacheEncryptionOptions"/>, and a no-op <see cref="ICacheValueEncryptor"/>
/// (replaced by <c>AesCacheValueEncryptor</c> when the Redis module enables encryption).
/// <para>
/// For L2 Redis distributed cache and Redis pub/sub backplane, add
/// <c>GranitCachingStackExchangeRedisModule</c> from <c>Granit.Caching.StackExchangeRedis</c>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitCachingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCaching();
}
