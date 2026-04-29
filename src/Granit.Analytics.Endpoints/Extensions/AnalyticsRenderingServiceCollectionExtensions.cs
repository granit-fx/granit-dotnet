using Granit.Analytics.Endpoints.Rendering;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Analytics.Endpoints.Extensions;

/// <summary>
/// DI registration helpers for the analytics-side widget rendering pipeline
/// — the KPI <see cref="IWidgetInstanceRenderer"/> plus the per-datasource
/// evaluators it dispatches to. Public so applications wiring dashboards have
/// a single, well-named hook (the rest of the rendering machinery stays
/// internal).
/// </summary>
public static class AnalyticsRenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <c>"Kpi"</c> widget renderer plus the bundled
    /// <see cref="IDatasourceEvaluator{TDatasource}"/> implementations for
    /// <see cref="MetricDatasource"/> (full), <see cref="QueryAggregateDatasource"/>
    /// (stub — returns Unavailable until the query-aggregate path lands), and
    /// <see cref="TelemetryDatasource"/> (stub — replaced by
    /// <c>Granit.IoT.Dashboards</c> when shipped).
    /// </summary>
    /// <remarks>
    /// Call AFTER <c>AddGranitAnalyticsEndpoints()</c> — the
    /// <see cref="MetricDatasourceEvaluator"/> resolves the runner registry
    /// surfaced by that registration.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsWidgetRenderers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IDatasourceEvaluator<MetricDatasource>, MetricDatasourceEvaluator>();
        services.TryAddSingleton<IDatasourceEvaluator<QueryAggregateDatasource>, QueryAggregateDatasourceEvaluator>();
        services.TryAddSingleton<IDatasourceEvaluator<TelemetryDatasource>, TelemetryDatasourceEvaluator>();

        services.AddScoped<IWidgetInstanceRenderer, KpiWidgetInstanceRenderer>();

        return services;
    }
}
