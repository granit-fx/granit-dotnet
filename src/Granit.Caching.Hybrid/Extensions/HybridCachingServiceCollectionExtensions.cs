using Granit.Caching;
using Granit.Caching.Hybrid.Options;
using Granit.Caching.Options;
using Granit.Timing.Extensions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Caching.Hybrid.Extensions;

/// <summary>
/// DI registration extensions for the HybridCache provider.
/// </summary>
public static class HybridCachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HybridCache provider (L1 local memory + L2 IDistributedCache/Redis).
    /// Overrides <see cref="ICacheService{TCacheItem}"/> with <see cref="HybridCacheService{TCacheItem}"/>.
    /// </summary>
    /// <remarks>
    /// Prerequisites: <c>AddGranitCaching()</c> and <c>AddGranitCachingRedis()</c> must be called
    /// before this method (via <c>GranitCachingModule</c> and <c>GranitCachingRedisModule</c>
    /// through the <c>[DependsOn]</c> attributes).
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitCachingHybrid(
        this IServiceCollection services)
    {
        // IClock is required by HybridCacheService; TryAdd is idempotent
        services.AddGranitTiming();

        services
            .AddOptions<HybridCachingOptions>()
            .BindConfiguration(HybridCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Reconfigure les options HybridCache enregistrées par GranitCachingModule
        // pour ajouter LocalCacheExpiration (L1 courte) en mode multi-pods.
        // Deferred configuration: reads CachingOptions and HybridCachingOptions at resolution time.
        services
            .AddOptions<HybridCacheOptions>()
            .Configure<IOptions<CachingOptions>, IOptions<HybridCachingOptions>>(
                (hybridCache, cachingOpts, hybridOpts) =>
                {
                    hybridCache.DefaultEntryOptions = new HybridCacheEntryOptions
                    {
                        // L2 (Redis) : expiration longue selon la config globale
                        Expiration = cachingOpts.Value.DefaultAbsoluteExpirationRelativeToNow,
                        // L1 (mémoire locale) : expiration courte pour limiter la staleness inter-pods
                        LocalCacheExpiration = hybridOpts.Value.LocalCacheExpiration,
                    };
                });

        // Surcharge ICacheService<T> avec HybridCacheService<T>
        // AddSingleton (pas TryAdd) pour remplacer l'enregistrement Memory de AddGranitCaching
        services.AddSingleton(typeof(ICacheService<>), typeof(HybridCacheService<>));

        return services;
    }
}
