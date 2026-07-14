using Granit.Http.Idempotency.StackExchangeRedis.Extensions;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;

namespace Granit.Http.Idempotency.StackExchangeRedis;

/// <summary>
/// Granit module for the Redis idempotency store.
/// Replaces the in-memory store registered by <see cref="GranitHttpIdempotencyModule"/>.
/// </summary>
/// <remarks>
/// <para>
/// TLS is enforced by default. Set <c>Http:Idempotency:Redis:RequireTls = false</c>
/// for local development without TLS.
/// </para>
/// <para>
/// When <c>Granit.Caching.StackExchangeRedis</c> is co-registered, the existing
/// <c>IConnectionMultiplexer</c> is reused (single TCP connection pool).
/// </para>
/// </remarks>
[DependsOn(typeof(GranitHttpIdempotencyModule))]
public sealed class GranitHttpIdempotencyStackExchangeRedisModule : GranitModule
{
    /// <inheritdoc/>
    public override bool IsEnabled(ServiceConfigurationContext context)
    {
        RedisIdempotencyOptions opts = context.Configuration
            .GetSection(RedisIdempotencyOptions.SectionName)
            .Get<RedisIdempotencyOptions>() ?? new RedisIdempotencyOptions();

        return opts.IsEnabled;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitRedisIdempotency();
}
