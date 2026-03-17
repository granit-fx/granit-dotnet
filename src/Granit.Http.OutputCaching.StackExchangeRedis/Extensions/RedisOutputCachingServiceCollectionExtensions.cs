using Granit.Caching.StackExchangeRedis.HealthChecks;
using Granit.Http.OutputCaching.StackExchangeRedis.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.Http.OutputCaching.StackExchangeRedis.Extensions;

/// <summary>
/// DI registration extensions for the Redis output cache store.
/// </summary>
public static class RedisOutputCachingServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the in-memory output cache store with Redis.
    /// TLS is enforced by default (<see cref="RedisOutputCachingOptions.RequireTls"/>).
    /// </summary>
    /// <remarks>
    /// When <c>Granit.Caching.StackExchangeRedis</c> is co-registered, the existing
    /// <see cref="IConnectionMultiplexer"/> singleton is reused to avoid duplicate TCP connections.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitRedisOutputCache(this IServiceCollection services)
    {
        services
            .AddOptions<RedisOutputCachingOptions>()
            .BindConfiguration(RedisOutputCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddStackExchangeRedisOutputCache(_ => { });

        services
            .AddOptions<Microsoft.AspNetCore.OutputCaching.StackExchangeRedis.RedisOutputCacheOptions>()
            .Configure<IOptions<RedisOutputCachingOptions>, IServiceProvider>((redis, granitOpts, sp) =>
            {
                RedisOutputCachingOptions opts = granitOpts.Value;

                // Reuse existing IConnectionMultiplexer from Granit.Caching.StackExchangeRedis
                // to avoid opening duplicate TCP connections to the same Redis instance.
                IConnectionMultiplexer? existingMultiplexer = sp.GetService<IConnectionMultiplexer>();
                if (existingMultiplexer is not null)
                {
                    redis.ConnectionMultiplexerFactory = () => Task.FromResult(existingMultiplexer);
                }
                else
                {
                    var config = ConfigurationOptions.Parse(opts.Configuration);
                    if (opts.RequireTls)
                    {
                        config.Ssl = true;
                    }

                    redis.ConnectionMultiplexerFactory = () =>
                        Task.FromResult<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(config));
                }

                redis.InstanceName = opts.InstanceName;
            });

        return services;
    }

    /// <summary>
    /// Adds a Redis output cache connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"redis-output-cache"</c>.</param>
    /// <param name="degradedThreshold">Latency above which the check returns Degraded. Defaults to 100 ms.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 5 seconds.</param>
    public static IHealthChecksBuilder AddGranitRedisOutputCacheHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "redis-output-cache",
        TimeSpan? degradedThreshold = null,
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        TimeSpan threshold = degradedThreshold ?? TimeSpan.FromMilliseconds(100);

        if (builder.Services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
        {
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                RedisOutputCachingOptions opts =
                    sp.GetRequiredService<IOptions<RedisOutputCachingOptions>>().Value;

                var config = ConfigurationOptions.Parse(opts.Configuration);
                if (opts.RequireTls)
                {
                    config.Ssl = true;
                }

                return ConnectionMultiplexer.Connect(config);
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
