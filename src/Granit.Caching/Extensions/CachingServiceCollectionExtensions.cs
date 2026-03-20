using Granit.Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Caching.Extensions;

/// <summary>
/// DI registration extensions for <c>Granit.Caching</c> (configuration and encryption only).
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers cache configuration options and a no-op <see cref="ICacheValueEncryptor"/>.
    /// </summary>
    /// <remarks>
    /// Registered services:
    /// <list type="bullet">
    ///   <item><see cref="CachingOptions"/> bound from <c>"Cache"</c> configuration section</item>
    ///   <item><see cref="CacheEncryptionOptions"/> bound from <c>"Cache:Encryption"</c> section</item>
    ///   <item><see cref="ICacheValueEncryptor"/> → <see cref="NullCacheValueEncryptor"/> (no-op by default)</item>
    /// </list>
    /// The <c>NullCacheValueEncryptor</c> is replaced by <c>AesCacheValueEncryptor</c> when
    /// <c>GranitCachingRedisModule</c> detects <c>EncryptValues = true</c>.
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

        // No-op encryptor by default (replaced by AesCacheValueEncryptor if EncryptValues=true)
        services.TryAddSingleton<ICacheValueEncryptor, NullCacheValueEncryptor>();

        // IDistributedCache (memory) for local development — replaced by Redis in production
        services.AddDistributedMemoryCache();

        return services;
    }
}
