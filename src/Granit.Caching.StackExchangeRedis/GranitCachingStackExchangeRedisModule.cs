using Granit.Caching.StackExchangeRedis.Extensions;
using Granit.Caching.StackExchangeRedis.Options;
using Granit.Modularity;
using Granit.Observability;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;

namespace Granit.Caching.StackExchangeRedis;

/// <summary>
/// Granit module for the Redis distributed cache provider.
/// Upgrades the FusionCache instance registered by <c>GranitCachingModule</c>
/// with L2 Redis distributed cache and a Redis pub/sub backplane for cross-pod
/// L1 invalidation.
/// </summary>
/// <remarks>
/// <para>
/// Enabling AES-256 encryption:
/// <code>
/// // appsettings.json
/// {
///   "Cache": {
///     "EncryptValues": true,
///     "Encryption": { "Key": "base64-key-from-vault" },
///     "Redis": { "Configuration": "redis:6379", "InstanceName": "app:" }
///   }
/// }
/// </code>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingModule))]
public sealed class GranitCachingStackExchangeRedisModule : GranitModule
{
    /// <inheritdoc/>
    public override bool IsEnabled(ServiceConfigurationContext context)
    {
        RedisCachingOptions redisOpts = context.Configuration
            .GetSection(RedisCachingOptions.SectionName)
            .Get<RedisCachingOptions>() ?? new RedisCachingOptions();

        return redisOpts.IsEnabled;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitCachingRedis();

        // Auto-wire StackExchange.Redis OTel instrumentation when Granit.Observability
        // is hosted. AddRedisInstrumentation() with no argument resolves the
        // IConnectionMultiplexer registered above from the service provider.
        GranitOpenTelemetryRegistry.RegisterTracing(t => t.AddRedisInstrumentation());
    }
}
