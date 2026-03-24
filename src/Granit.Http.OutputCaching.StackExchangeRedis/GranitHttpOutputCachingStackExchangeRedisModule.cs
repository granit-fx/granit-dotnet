using Granit.Http.OutputCaching.StackExchangeRedis.Extensions;
using Granit.Http.OutputCaching.StackExchangeRedis.Options;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;

namespace Granit.Http.OutputCaching.StackExchangeRedis;

/// <summary>
/// Granit module for the Redis output cache store.
/// Replaces the in-memory store registered by <see cref="GranitHttpOutputCachingModule"/>.
/// </summary>
/// <remarks>
/// <para>
/// TLS is enforced by default. Set <c>OutputCaching:Redis:RequireTls = false</c>
/// for local development without TLS.
/// </para>
/// <para>
/// When <c>Granit.Caching.StackExchangeRedis</c> is co-registered, the existing
/// <c>IConnectionMultiplexer</c> is reused (single TCP connection pool).
/// </para>
/// </remarks>
[DependsOn(typeof(GranitHttpOutputCachingModule))]
public sealed class GranitHttpOutputCachingStackExchangeRedisModule : GranitModule
{
    /// <inheritdoc/>
    public override bool IsEnabled(ServiceConfigurationContext context)
    {
        RedisOutputCachingOptions opts = context.Configuration
            .GetSection(RedisOutputCachingOptions.SectionName)
            .Get<RedisOutputCachingOptions>() ?? new RedisOutputCachingOptions();

        return opts.IsEnabled;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitRedisOutputCache();
}
