using Granit.Caching;
using Granit.Caching.Options;
using Granit.Caching.StackExchangeRedis.HealthChecks;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.StackExchangeRedis.Internal;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.Http.Idempotency.StackExchangeRedis.Extensions;

/// <summary>
/// DI registration extensions for the Redis idempotency store.
/// </summary>
public static partial class RedisIdempotencyServiceCollectionExtensions
{
    [LoggerMessage(Level = LogLevel.Warning, Message =
        "Redis idempotency store is active but no Cache:Encryption:Key is resolvable — " +
        "idempotency entries (replayable HTTP responses) would be stored in plaintext in Redis. " +
        "This is tolerated only in Development; a startup validator fails the host in any other " +
        "environment. Provide a base64 256-bit Cache:Encryption:Key.")]
    private static partial void LogIdempotencyEncryptionDisabled(ILogger logger);

    /// <summary>
    /// Replaces the in-memory idempotency store with Redis.
    /// TLS is enforced by default (<see cref="RedisIdempotencyOptions.RequireTls"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>Granit.Caching.StackExchangeRedis</c> (or any other package) has already
    /// registered an <see cref="IConnectionMultiplexer"/>, that singleton is reused to avoid
    /// duplicate TCP connections; otherwise one is created from
    /// <see cref="RedisIdempotencyOptions.Configuration"/>.
    /// </para>
    /// <para>
    /// Entries are encrypted unconditionally via <see cref="ICacheValueEncryptor"/> —
    /// AES-256-GCM whenever a <c>Cache:Encryption:Key</c> resolves. Without a key the no-op
    /// encryptor is tolerated in Development only; elsewhere
    /// <see cref="RedisIdempotencyEncryptionStartupValidator"/> fails startup.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitRedisIdempotency(this IServiceCollection services)
    {
        services
            .AddOptions<RedisIdempotencyOptions>()
            .BindConfiguration(RedisIdempotencyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Same section Granit.Caching binds — re-bound here so the AES key resolves even when
        // the caching module is not installed. Double binding of one section is harmless.
        services
            .AddOptions<CacheEncryptionOptions>()
            .BindConfiguration(CacheEncryptionOptions.SectionName);

        // Secure-by-default encryptor: AES-256-GCM whenever a key resolves. TryAdd keeps
        // Granit.Caching.StackExchangeRedis's own (equivalent) registration authoritative when
        // both packages are installed, regardless of module registration order.
        services.TryAddSingleton<ICacheValueEncryptor>(sp =>
        {
            CacheEncryptionOptions encryptionOpts = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(encryptionOpts.Key))
            {
                return ActivatorUtilities.CreateInstance<AesCacheValueEncryptor>(sp);
            }

            ILogger? logger = sp.GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(RedisIdempotencyServiceCollectionExtensions));
            if (logger is not null)
            {
                LogIdempotencyEncryptionDisabled(logger);
            }

            return new NullCacheValueEncryptor();
        });

        // Fail-closed gate: outside Development, refuse to start when entries would land in
        // Redis unencrypted (see the validator's remarks for why there is no opt-out flag).
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<RedisIdempotencyOptions>, RedisIdempotencyEncryptionStartupValidator>());

        // Reuse an existing IConnectionMultiplexer (Granit.Caching.StackExchangeRedis) when
        // present — single TCP connection pool per Redis instance.
        if (services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                RedisIdempotencyOptions opts = sp.GetRequiredService<IOptions<RedisIdempotencyOptions>>().Value;
                return ConnectionMultiplexer.Connect(BuildConfiguration(opts));
            });
        }

        // Deterministic replacement of the in-memory default registered by
        // Granit.Http.Idempotency (TryAddSingleton): Replace removes that descriptor and adds
        // ours; when called before AddGranitIdempotency, the base TryAdd then no-ops.
        services.Replace(ServiceDescriptor.Singleton<IIdempotencyStore, RedisIdempotencyStore>());

        return services;
    }

    /// <summary>
    /// Adds a Redis idempotency-store connectivity health check tagged <c>"readiness"</c> and
    /// <c>"startup"</c>. Issues a PING and measures round-trip latency.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"redis-idempotency"</c>.</param>
    /// <param name="degradedThreshold">Latency above which the check returns Degraded. Defaults to 100 ms.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 5 seconds.</param>
    public static IHealthChecksBuilder AddGranitRedisIdempotencyHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "redis-idempotency",
        TimeSpan? degradedThreshold = null,
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        TimeSpan threshold = degradedThreshold ?? TimeSpan.FromMilliseconds(100);

        if (builder.Services.All(d => d.ServiceType != typeof(IConnectionMultiplexer)))
        {
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                RedisIdempotencyOptions opts = sp.GetRequiredService<IOptions<RedisIdempotencyOptions>>().Value;
                return ConnectionMultiplexer.Connect(BuildConfiguration(opts));
            });
        }

        // The RedisHealthCheck instance is created inside the registration factory rather than
        // registered as a singleton service: other Granit Redis packages register the same
        // concrete type, and a DI-level registration would let the last one win for all checks.
        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new RedisHealthCheck(sp.GetRequiredService<IConnectionMultiplexer>(), threshold),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(5)));
    }

    private static ConfigurationOptions BuildConfiguration(RedisIdempotencyOptions opts)
    {
        var config = ConfigurationOptions.Parse(opts.Configuration);
        if (opts.RequireTls)
        {
            config.Ssl = true;
        }

        return config;
    }
}
