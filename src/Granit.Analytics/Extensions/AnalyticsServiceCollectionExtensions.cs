using Granit.Analytics.Diagnostics;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Analytics.Rendering;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;
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
        services.TryAddSingleton<AnalyticsRuntimeMetrics>();
        GranitActivitySourceRegistry.Register(AnalyticsActivitySource.Name);

        // Pure orchestration registries — name → runner dictionaries built from the
        // IEnumerable<I*Runner> populated by AddGranitAnalyticsRunners() in the EF Core
        // package. No EF Core dependency, so they live here in the main module.
        services.TryAddScoped<QueryAggregateService>();
        services.TryAddScoped<TableService>();
        services.TryAddScoped<ChartService>();
        services.TryAddScoped<PivotService>();
        services.TryAddScoped<MapService>();

        // Period resolution — pure domain logic on IClock, no HTTP coupling.
        services.TryAddScoped<PeriodResolver>();

        // Domain renderers + non-HTTP datasource evaluators. The Metric evaluator
        // (FusionCache + period orchestration) lives in Granit.Analytics.Endpoints
        // and is wired by AddGranitAnalyticsWidgetRenderers() — Kpi rendering with
        // a MetricDatasource only succeeds when both calls happen.
        services.TryAddScoped<IDatasourceEvaluator<QueryAggregateDatasource>, QueryAggregateDatasourceEvaluator>();
        services.TryAddSingleton<IDatasourceEvaluator<TelemetryDatasource>, TelemetryDatasourceEvaluator>();

        services.AddScoped<IWidgetInstanceRenderer, KpiWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, TableWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, ChartWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, PivotWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, MapWidgetInstanceRenderer>();

        return services;
    }
}
