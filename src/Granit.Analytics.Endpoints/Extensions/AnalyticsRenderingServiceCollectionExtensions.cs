using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Rendering;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine;
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
        services.TryAddScoped<IDatasourceEvaluator<QueryAggregateDatasource>, QueryAggregateDatasourceEvaluator>();
        services.TryAddSingleton<IDatasourceEvaluator<TelemetryDatasource>, TelemetryDatasourceEvaluator>();

        // Query-aggregate runner registry — built once at startup. Iterates every
        // registered IQueryDefinitionDescriptor and closes QueryAggregateRunner<T>
        // over each entity type, mirroring how AnalyticsEndpointsServiceCollectionExtensions
        // builds metric runners. Keeps request-time dispatch reflection-free.
        services.TryAddScoped<QueryAggregateService>();

        foreach (ServiceDescriptor descriptor in services
            .Where(d => d.ServiceType == typeof(IQueryDefinitionDescriptor))
            .ToList())
        {
            services.AddSingleton<IQueryAggregateRunner>(sp =>
            {
                var d = (IQueryDefinitionDescriptor)
                    (descriptor.ImplementationFactory?.Invoke(sp)
                     ?? throw new InvalidOperationException(
                         "IQueryDefinitionDescriptor must be registered with an implementation factory."));

                Type runnerType = typeof(QueryAggregateRunner<>).MakeGenericType(d.EntityType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));

                return (IQueryAggregateRunner)Activator.CreateInstance(runnerType, d.Name, queryableSource)!;
            });
        }

        services.AddScoped<IWidgetInstanceRenderer, KpiWidgetInstanceRenderer>();

        return services;
    }
}
