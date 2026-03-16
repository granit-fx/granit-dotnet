using Granit.Caching.Options;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Caching.Extensions;

/// <summary>
/// DI registration extensions for <c>Granit.Caching</c>.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Granit cache system with the default <c>MemoryDistributedCache</c> provider.
    /// </summary>
    /// <remarks>
    /// Registered services:
    /// <list type="bullet">
    ///   <item><see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> → <c>MemoryDistributedCache</c> (replaced by Redis/Hybrid providers if loaded)</item>
    ///   <item>Dedicated <see cref="IMemoryCache"/> (key <c>Granit.Caching.Locks</c>) for stampede protection</item>
    ///   <item><see cref="ICacheValueEncryptor"/> → <see cref="NullCacheValueEncryptor"/> (no-op by default)</item>
    ///   <item><see cref="ICacheService{TCacheItem}"/> → <see cref="DistributedCacheService{TCacheItem}"/></item>
    ///   <item><see cref="ICacheService{TCacheItem, TKey}"/> → <see cref="TypedKeyCacheServiceAdapter{TCacheItem, TKey}"/></item>
    /// </list>
    /// To enable AES encryption, register <see cref="AesCacheValueEncryptor"/> after this call
    /// and configure <c>Cache:Encryption:Key</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitCaching(
        this IServiceCollection services)
    {
        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<CacheEncryptionOptions>()
            .BindConfiguration(CacheEncryptionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Fournisseur Memory par défaut (remplacé par les modules Redis/Hybrid si chargés après)
        services.AddDistributedMemoryCache();

        // IMemoryCache dédié aux verrous stampede (séparé du cache applicatif)
        services.AddKeyedSingleton<IMemoryCache>(
            DistributedCacheService<object>.LockCacheKey,
            (_, _) => new MemoryCache(Microsoft.Extensions.Options.Options.Create(new MemoryCacheOptions
            {
                // Limite à 10 000 verrous simultanés maximum
                SizeLimit = 10_000
            })));

        // Chiffreur no-op par défaut (remplacé par AesCacheValueEncryptor si EncryptValues=true)
        services.TryAddSingleton<ICacheValueEncryptor, NullCacheValueEncryptor>();

        services.TryAddSingleton(typeof(ICacheService<>), typeof(DistributedCacheService<>));
        services.TryAddSingleton(typeof(ICacheService<,>), typeof(TypedKeyCacheServiceAdapter<,>));

        // HybridCache memory-only par défaut (L1 uniquement, pas de L2)
        // GranitCachingHybridModule reconfigure les options pour ajouter L2 Redis + LocalCacheExpiration
        services.AddHybridCache();
        services
            .AddOptions<HybridCacheOptions>()
            .Configure<IOptions<CachingOptions>>((hybrid, cachingOpts) =>
            {
                hybrid.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = cachingOpts.Value.DefaultAbsoluteExpirationRelativeToNow,
                };
            });

        return services;
    }
}
