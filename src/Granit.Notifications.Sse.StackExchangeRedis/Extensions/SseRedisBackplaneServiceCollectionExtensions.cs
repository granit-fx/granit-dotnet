using Granit.Extensions;
using Granit.Notifications.Sse.StackExchangeRedis.Diagnostics;
using Granit.Notifications.Sse.StackExchangeRedis.Internal;
using Granit.Notifications.Sse.StackExchangeRedis.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Notifications.Sse.StackExchangeRedis.Extensions;

/// <summary>Registers the Redis pub/sub backplane for the SSE notification channel.</summary>
public static class SseRedisBackplaneServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SSE Redis backplane. Requires an <c>IConnectionMultiplexer</c> singleton
    /// (e.g. from <c>Granit.Caching.StackExchangeRedis</c> or host-provided).
    /// </summary>
    public static IServiceCollection AddGranitNotificationsSseRedisBackplane(
        this IServiceCollection services,
        Action<SseRedisBackplaneOptions>? configure = null)
    {
        services.AddGranitProviderOptions<SseRedisBackplaneOptions>(SseRedisBackplaneOptions.SectionName);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<SseRedisBackplaneMetrics>();
        services.TryAddSingleton<RedisSseBackplane>();
        services.TryAddSingleton<ISseBackplane>(sp => sp.GetRequiredService<RedisSseBackplane>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RedisSseBackplane>());

        return services;
    }
}
