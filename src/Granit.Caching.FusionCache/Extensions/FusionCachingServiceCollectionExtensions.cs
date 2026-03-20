using Granit.Caching.FusionCache.Internal;
using Granit.Caching.FusionCache.Options;
using Granit.Caching.Options;
using Granit.Caching.StackExchangeRedis.Options;
using Granit.Timing.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Caching.FusionCache.Extensions;

/// <summary>
/// DI registration extensions for the FusionCache provider.
/// </summary>
public static class FusionCachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers FusionCache as the default <see cref="IFusionCache"/> with L1+L2+backplane+OpenTelemetry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prerequisites: <c>AddGranitCaching()</c> and <c>AddGranitCachingRedis()</c> must be called
    /// before this method (via module dependencies).
    /// </para>
    /// <para>
    /// L2 distributed cache: reuses the <c>IDistributedCache</c> registered by
    /// <c>GranitCachingRedisModule</c>.
    /// </para>
    /// <para>
    /// Backplane: Redis pub/sub for cross-pod L1 invalidation. Only registered when
    /// <c>RedisCachingOptions.IsEnabled</c> is <c>true</c>.
    /// </para>
    /// <para>
    /// Encryption: <see cref="EncryptingFusionCacheSerializer"/> wraps the
    /// <c>SystemTextJson</c> serializer with AES-256-CBC when <c>CachingOptions.EncryptValues</c>
    /// is <c>true</c>. Applied only to L2 (Redis) — L1 stores objects in cleartext.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitCachingFusionCache(
        this IServiceCollection services)
    {
        services.AddGranitTiming();

        services
            .AddOptions<FusionCachingOptions>()
            .BindConfiguration(FusionCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Ensure IConnectionMultiplexer is registered (may already be from health check)
        if (services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                RedisCachingOptions opts = sp.GetRequiredService<IOptions<RedisCachingOptions>>().Value;
                return ConnectionMultiplexer.Connect(opts.Configuration);
            });
        }

        // Register FusionCache with deferred option configuration
        services.AddFusionCache()
            .WithRegisteredDistributedCache()
            .WithSystemTextJsonSerializer();

        // Deferred configuration: resolve options at runtime to configure FusionCache
        services.AddOptions<FusionCacheOptions>()
            .Configure<IOptions<CachingOptions>, IOptions<FusionCachingOptions>, IOptions<RedisCachingOptions>>(
                (fc, cachingOpts, fusionOpts, redisOpts) =>
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

        // Backplane: Redis pub/sub for cross-pod L1 invalidation
        services.AddFusionCacheStackExchangeRedisBackplane(_ => { });

        // Deferred backplane configuration via options
        services.AddOptions<ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplaneOptions>()
            .Configure<IConnectionMultiplexer>(
                (backplane, mux) =>
                {
                    backplane.ConnectionMultiplexerFactory = () => Task.FromResult(mux);
                });

        return services;
    }
}
