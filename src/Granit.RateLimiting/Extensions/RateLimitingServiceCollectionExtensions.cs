using Granit.Core.Diagnostics;
using Granit.Http.ExceptionHandling;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Diagnostics;
using Granit.RateLimiting.Exceptions;
using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.RateLimiting.Extensions;

/// <summary>
/// Extension methods for registering Granit rate limiting services.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Registers Granit rate limiting services using the <c>"RateLimiting"</c> configuration section.
    /// </summary>
    public static IServiceCollection AddGranitRateLimiting(
        this IServiceCollection services,
        Action<GranitRateLimitingOptions>? configure = null)
    {
        OptionsBuilder<GranitRateLimitingOptions> optionsBuilder = services
            .AddOptions<GranitRateLimitingOptions>()
            .BindConfiguration(GranitRateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddGranitRateLimitingCore();
    }

    /// <summary>
    /// Registers Granit rate limiting services using a configuration section.
    /// </summary>
    public static IServiceCollection AddGranitRateLimiting(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        services
            .AddOptions<GranitRateLimitingOptions>()
            .Bind(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddGranitRateLimitingCore();
    }

    private static IServiceCollection AddGranitRateLimitingCore(this IServiceCollection services)
    {
        // Options validation
        services.AddSingleton<IValidateOptions<GranitRateLimitingOptions>, GranitRateLimitingOptionsValidator>();

        // Counter store: Redis if IConnectionMultiplexer is available, otherwise in-memory
        services.TryAddScoped<IRateLimitCounterStore>(sp =>
        {
            StackExchange.Redis.IConnectionMultiplexer? redis =
                sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();

            if (redis is not null)
            {
                return new RedisRateLimitCounterStore(
                    redis,
                    sp.GetRequiredService<IOptions<GranitRateLimitingOptions>>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RedisRateLimitCounterStore>>());
            }

            return new InMemoryRateLimitCounterStore(
                sp.GetRequiredService<TimeProvider>());
        });

        // Quota provider: feature-based or static options
        services.TryAddScoped<IRateLimitQuotaProvider>(sp =>
        {
            GranitRateLimitingOptions opts = sp.GetRequiredService<IOptions<GranitRateLimitingOptions>>().Value;

            if (opts.UseFeatureBasedQuotas)
            {
                return new FeatureBasedRateLimitQuotaProvider(
                    sp.GetRequiredService<IOptionsMonitor<GranitRateLimitingOptions>>(),
                    sp);
            }

            return new OptionsRateLimitQuotaProvider(
                sp.GetRequiredService<IOptionsMonitor<GranitRateLimitingOptions>>());
        });

        // Core services
        services.TryAddScoped<TenantPartitionedRateLimiter>();
        services.TryAddSingleton<RateLimitingMetrics>();

        // Exception status code mapping (429)
        services.AddSingleton<IExceptionStatusCodeMapper, RateLimitExceptionStatusCodeMapper>();

        // ActivitySource registration
        GranitActivitySourceRegistry.Register(RateLimitingActivitySource.Name);

        return services;
    }
}
