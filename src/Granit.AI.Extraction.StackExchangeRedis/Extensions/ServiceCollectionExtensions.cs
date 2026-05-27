using Granit.AI.Extraction.RateLimiting;
using Granit.AI.Extraction.StackExchangeRedis.Internal;
using Granit.AI.Extraction.StackExchangeRedis.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Extraction.StackExchangeRedis.Extensions;

/// <summary>
/// Registration helpers for the Redis-backed <see cref="IAICallRateLimiter"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the in-memory <see cref="IAICallRateLimiter"/> registered by
    /// <c>Granit.AI.Extraction</c> with the distributed <see cref="RedisAICallRateLimiter"/>.
    /// </summary>
    /// <remarks>
    /// Requires an <see cref="StackExchange.Redis.IConnectionMultiplexer"/> in the container (typically the one
    /// registered by <c>Granit.Caching.StackExchangeRedis</c>). Binds
    /// <see cref="AIRateLimitingRedisOptions"/> from configuration and validates it at startup.
    /// </remarks>
    public static IServiceCollection AddGranitAIExtractionRedisRateLimiter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AIRateLimitingRedisOptions>()
            .BindConfiguration(AIRateLimitingRedisOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Replace, not TryAdd: the in-memory AICallRateLimiter is registered by
        // GranitAIExtractionModule via TryAddSingleton; we deliberately override it.
        services.Replace(ServiceDescriptor.Singleton<IAICallRateLimiter, RedisAICallRateLimiter>());

        return services;
    }
}
