using Granit.Diagnostics;
using Granit.Http.Bulkhead.Abstractions;
using Granit.Http.Bulkhead.Diagnostics;
using Granit.Http.Bulkhead.Exceptions;
using Granit.Http.Bulkhead.Internal;
using Granit.Http.Bulkhead.Options;
using Granit.Http.ExceptionHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Http.Bulkhead.Extensions;

/// <summary>
/// Extension methods for registering Granit bulkhead isolation services.
/// </summary>
public static class BulkheadServiceCollectionExtensions
{
    /// <summary>
    /// Registers Granit bulkhead isolation services using the <c>"Bulkhead"</c> configuration section.
    /// </summary>
    public static IServiceCollection AddGranitBulkhead(
        this IServiceCollection services,
        Action<GranitBulkheadOptions>? configure = null)
    {
        OptionsBuilder<GranitBulkheadOptions> optionsBuilder = services
            .AddOptions<GranitBulkheadOptions>()
            .BindConfiguration(GranitBulkheadOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddGranitBulkheadCore();
    }

    /// <summary>
    /// Registers Granit bulkhead isolation services using a configuration section.
    /// </summary>
    public static IServiceCollection AddGranitBulkhead(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        services
            .AddOptions<GranitBulkheadOptions>()
            .Bind(configurationSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddGranitBulkheadCore();
    }

    private static IServiceCollection AddGranitBulkheadCore(this IServiceCollection services)
    {
        // ActivitySource registration
        GranitActivitySourceRegistry.Register(BulkheadActivitySource.Name);

        // Options validation
        services.AddSingleton<IValidateOptions<GranitBulkheadOptions>, GranitBulkheadOptionsValidator>();

        // Core singleton registry
        services.TryAddSingleton<ConcurrencyLimiterRegistry>();

        // Quota provider: feature-based or static options
        services.TryAddScoped<IBulkheadQuotaProvider>(sp =>
        {
            GranitBulkheadOptions opts = sp.GetRequiredService<IOptions<GranitBulkheadOptions>>().Value;

            if (opts.UseFeatureBasedQuotas)
            {
                return ActivatorUtilities.CreateInstance<FeatureBasedBulkheadQuotaProvider>(sp);
            }

            return new OptionsBulkheadQuotaProvider(
                sp.GetRequiredService<IOptionsMonitor<GranitBulkheadOptions>>());
        });

        // Scoped orchestrator
        services.TryAddScoped<TenantPartitionedBulkhead>();

        // Metrics (singleton)
        services.TryAddSingleton<BulkheadMetrics>();

        // Exception status code mapping (503)
        services.AddSingleton<IExceptionStatusCodeMapper, BulkheadExceptionStatusCodeMapper>();

        // Background cleanup service
        services.AddHostedService<BulkheadCleanupService>();

        return services;
    }
}
