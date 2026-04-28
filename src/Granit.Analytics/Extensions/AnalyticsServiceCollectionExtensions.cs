using Granit.Analytics.Diagnostics;
using Granit.Analytics.Metrics;
using Granit.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Analytics.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Analytics</c> declarative primitives.
/// </summary>
/// <remarks>
/// Definitions are pure declarations and do not require a runtime — only hosts that
/// execute metrics need <c>Granit.Analytics.EntityFrameworkCore</c>.
/// </remarks>
public static class AnalyticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core Granit.Analytics declarative primitives:
    /// <see cref="AnalyticsMetrics"/>, the <c>Granit.Analytics</c> ActivitySource registration,
    /// and the metric definition registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalytics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<AnalyticsMetrics>();
        GranitActivitySourceRegistry.Register(AnalyticsActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Registers a metric definition for the specified entity and value types.
    /// </summary>
    /// <typeparam name="TEntity">The target entity type.</typeparam>
    /// <typeparam name="TValue">The aggregation result type (<c>int</c>, <c>long</c>, <c>decimal</c>, <c>double</c>).</typeparam>
    /// <typeparam name="TDefinition">The metric definition implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMetricDefinition<TEntity, TValue, TDefinition>(
        this IServiceCollection services)
        where TEntity : class
        where TValue : struct
        where TDefinition : MetricDefinition<TEntity, TValue>, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<MetricDefinition<TEntity, TValue>>(_ => new TDefinition());
        services.AddSingleton<IMetricDefinitionDescriptor>(sp =>
            sp.GetRequiredService<MetricDefinition<TEntity, TValue>>());

        return services;
    }
}
