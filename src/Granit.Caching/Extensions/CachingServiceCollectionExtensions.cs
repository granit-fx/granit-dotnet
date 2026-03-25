using Granit.Caching.Diagnostics;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Granit.Timing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Caching.Extensions;

/// <summary>
/// DI registration extensions for <c>Granit.Caching</c>.
/// </summary>
public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers cache configuration, FusionCache (L1 in-memory), encryption, and OpenTelemetry.
    /// </summary>
    /// <remarks>
    /// Registered services:
    /// <list type="bullet">
    ///   <item><see cref="CachingOptions"/> bound from <c>"Cache"</c> configuration section</item>
    ///   <item><see cref="CacheEncryptionOptions"/> bound from <c>"Cache:Encryption"</c> section</item>
    ///   <item><see cref="FusionCachingOptions"/> bound from <c>"Cache:FusionCache"</c> section</item>
    ///   <item><see cref="ICacheValueEncryptor"/> → <see cref="NullCacheValueEncryptor"/> (no-op by default)</item>
    ///   <item><c>IFusionCache</c> with L1 in-memory cache, fail-safe, factory timeouts, eager refresh</item>
    ///   <item><see cref="EncryptingFusionCacheSerializer"/> wrapping SystemTextJson for L2 encryption</item>
    /// </list>
    /// <para>
    /// For L2 Redis + backplane, add <c>Granit.Caching.StackExchangeRedis</c> which upgrades
    /// the FusionCache instance with <c>WithRegisteredDistributedCache()</c> and a Redis backplane.
    /// </para>
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
        services.TryAddSingleton<CachingMetrics>();

        // IDistributedCache (memory) for local development — replaced by Redis in production
        services.AddDistributedMemoryCache();

        // IClock for time-based operations
        services.AddGranitTiming();

        // FusionCachingOptions (fail-safe, timeouts, eager refresh, backplane prefix)
        services
            .AddOptions<FusionCachingOptions>()
            .BindConfiguration(FusionCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register FusionCache with L1 in-memory + SystemTextJson serialization
        services.AddFusionCache()
            .WithSystemTextJsonSerializer();

        // Deferred configuration: resolve options at runtime to configure FusionCache defaults
        services.AddOptions<FusionCacheOptions>()
            .Configure<IOptions<CachingOptions>, IOptions<FusionCachingOptions>>(
                (fc, cachingOpts, fusionOpts) =>
                {
                    fc.DefaultEntryOptions = new FusionCacheEntryOptions
                    {
                        Duration = cachingOpts.Value.DefaultAbsoluteExpirationRelativeToNow
                            ?? TimeSpan.FromHours(1),
                        IsFailSafeEnabled = fusionOpts.Value.FailSafeIsEnabled,
                        FailSafeMaxDuration = fusionOpts.Value.FailSafeMaxDuration,
                        FailSafeThrottleDuration = fusionOpts.Value.FailSafeThrottleDuration,
                        FactorySoftTimeout = fusionOpts.Value.FactorySoftTimeout,
                        FactoryHardTimeout = fusionOpts.Value.FactoryHardTimeout,
                        EagerRefreshThreshold = fusionOpts.Value.EagerRefreshThreshold,
                    };

                    fc.CacheKeyPrefix = cachingOpts.Value.KeyPrefix + ":";
                    fc.BackplaneChannelPrefix = fusionOpts.Value.BackplaneChannelPrefix;
                    fc.EnableAutoRecovery = true;
                });

        // Replace the serializer with the encrypting decorator
        services.AddSingleton<ZiggyCreatures.Caching.Fusion.Serialization.IFusionCacheSerializer>(sp =>
        {
            CachingOptions cachingOpts = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
            ICacheValueEncryptor encryptor = sp.GetRequiredService<ICacheValueEncryptor>();
            var jsonSerializer = new FusionCacheSystemTextJsonSerializer(cachingOpts.JsonOptions);
            return new EncryptingFusionCacheSerializer(jsonSerializer, encryptor, cachingOpts.EncryptValues);
        });

        return services;
    }
}
