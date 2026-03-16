using Granit.Caching;
using Granit.Caching.Options;
using Granit.Caching.StackExchangeRedis.HealthChecks;
using Granit.Caching.StackExchangeRedis.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.Caching.StackExchangeRedis.Extensions;

/// <summary>
/// DI registration extensions for the Redis cache provider.
/// </summary>
public static class RedisCachingServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the Memory provider with Redis as the <c>IDistributedCache</c>.
    /// Enables AES-256 encryption (<see cref="AesCacheValueEncryptor"/>) when
    /// <c>CachingOptions.EncryptValues = true</c>.
    /// </summary>
    /// <remarks>
    /// The <c>IsEnabled</c> check is handled by the module (<c>GranitCachingRedisModule</c>).
    /// Calling this method always registers the Redis provider.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitCachingRedis(
        this IServiceCollection services)
    {
        services
            .AddOptions<RedisCachingOptions>()
            .BindConfiguration(RedisCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Remplace IDistributedCache (MemoryDistributedCache → RedisCache)
        // Deferred configuration: reads RedisCachingOptions at resolution time.
        services.AddStackExchangeRedisCache(_ => { });
        services
            .AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IOptions<RedisCachingOptions>>((redis, granitOpts) =>
            {
                redis.Configuration = granitOpts.Value.Configuration;
                redis.InstanceName = granitOpts.Value.InstanceName;
            });

        // Conditional AES-256 encryption: factory resolves at runtime based on CachingOptions.EncryptValues.
        services.AddSingleton<ICacheValueEncryptor>(sp =>
        {
            CachingOptions cachingOpts = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
            if (cachingOpts.EncryptValues)
            {
                return ActivatorUtilities.CreateInstance<AesCacheValueEncryptor>(sp);
            }

            return new NullCacheValueEncryptor();
        });

        return services;
    }

    /// <summary>
    /// Adds a Redis connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Issues a PING command and measures round-trip latency. Returns <c>Degraded</c>
    /// when latency exceeds <paramref name="degradedThreshold"/> and <c>Unhealthy</c>
    /// when Redis is unreachable.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="IConnectionMultiplexer"/> as a singleton if not already registered.
    /// Call <see cref="AddGranitCachingRedis"/> before this method so that
    /// <see cref="RedisCachingOptions"/> is configured.
    /// </remarks>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"redis"</c>.</param>
    /// <param name="degradedThreshold">Latency above which the check returns Degraded. Defaults to 100 ms.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 5 seconds.</param>
    public static IHealthChecksBuilder AddGranitRedisHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "redis",
        TimeSpan? degradedThreshold = null,
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        TimeSpan threshold = degradedThreshold ?? TimeSpan.FromMilliseconds(100);

        if (builder.Services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
        {
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                RedisCachingOptions opts = sp.GetRequiredService<IOptions<RedisCachingOptions>>().Value;
                return ConnectionMultiplexer.Connect(opts.Configuration);
            });
        }

        builder.Services.AddSingleton(sp =>
            new RedisHealthCheck(sp.GetRequiredService<IConnectionMultiplexer>(), threshold));

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<RedisHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(5)));
    }
}
