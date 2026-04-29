using Granit.Analytics.Endpoints.Diagnostics;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Rendering;
using Granit.Dashboards;
using Granit.Dashboards.Rendering;
using Granit.MultiTenancy;
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
    /// Registers the data-bound widget renderers shipped by
    /// <c>Granit.Analytics.Endpoints</c>:
    /// <list type="bullet">
    ///   <item><c>"Kpi"</c> — dispatches on <c>Datasource.Kind</c> via the
    ///         per-source <see cref="IDatasourceEvaluator{TDatasource}"/>
    ///         registry (Metric / QueryAggregate / Telemetry).</item>
    ///   <item><c>"Table"</c> — runs the named <c>QueryDefinition</c>
    ///         through the QueryEngine pipeline, projects the first
    ///         <c>pageSize</c> rows.</item>
    ///   <item><c>"Chart"</c> — runs the named <c>QueryDefinition</c>'s
    ///         <c>ExecuteGroupedAsync</c> pipeline, returns one bucket per
    ///         group (Count via SQL, numeric aggregations via SQL-level
    ///         GroupBy + Select expression trees).</item>
    ///   <item><c>"Pivot"</c> — streams the named <c>QueryDefinition</c>
    ///         through <c>ExecuteStreamAsync</c> and pivots in-memory by
    ///         a (row-tuple × column-tuple) composite key. Bounded by the
    ///         QueryDefinition's <c>MaxStreamSize</c>.</item>
    ///   <item><c>"Map"</c> — streams the named <c>QueryDefinition</c> and
    ///         projects each row into a map marker. B7-2 ships the
    ///         <c>LatLng</c> path (decimal columns); the PostGIS
    ///         <c>Geography</c> path is deferred and surfaces Unavailable.</item>
    /// </list>
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

        services.TryAddSingleton<AnalyticsEndpointsMetrics>();

        services.TryAddScoped<IDatasourceEvaluator<MetricDatasource>, MetricDatasourceEvaluator>();
        services.TryAddScoped<IDatasourceEvaluator<QueryAggregateDatasource>, QueryAggregateDatasourceEvaluator>();
        services.TryAddSingleton<IDatasourceEvaluator<TelemetryDatasource>, TelemetryDatasourceEvaluator>();

        // Per-QueryDefinition runner registries — built once at startup. Iterates
        // every registered IQueryDefinitionDescriptor and closes the runner
        // generics over each entity type, mirroring how
        // AnalyticsEndpointsServiceCollectionExtensions builds metric runners.
        // Keeps request-time dispatch reflection-free.
        services.TryAddScoped<QueryAggregateService>();
        services.TryAddScoped<TableService>();
        services.TryAddScoped<ChartService>();
        services.TryAddScoped<PivotService>();
        services.TryAddScoped<MapService>();

        foreach (ServiceDescriptor descriptor in services
            .Where(d => d.ServiceType == typeof(IQueryDefinitionDescriptor))
            .ToList())
        {
            services.AddScoped<IQueryAggregateRunner>(sp =>
            {
                IQueryDefinitionDescriptor d = ResolveDescriptor(sp, descriptor);
                Type runnerType = typeof(QueryAggregateRunner<>).MakeGenericType(d.EntityType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));
                object engine = sp.GetRequiredService(
                    typeof(IQueryEngine<>).MakeGenericType(d.EntityType));
                object definition = sp.GetRequiredService(
                    typeof(QueryDefinition<>).MakeGenericType(d.EntityType));

                return (IQueryAggregateRunner)Activator.CreateInstance(runnerType, d.Name, queryableSource, engine, definition)!;
            });

            services.AddScoped<ITableRunner>(sp =>
            {
                IQueryDefinitionDescriptor d = ResolveDescriptor(sp, descriptor);
                Type runnerType = typeof(TableRunner<>).MakeGenericType(d.EntityType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));
                object engine = sp.GetRequiredService(
                    typeof(IQueryEngine<>).MakeGenericType(d.EntityType));
                object definition = sp.GetRequiredService(
                    typeof(QueryDefinition<>).MakeGenericType(d.EntityType));

                return (ITableRunner)Activator.CreateInstance(
                    runnerType, d.Name, queryableSource, engine, definition)!;
            });

            services.AddScoped<IChartRunner>(sp =>
            {
                IQueryDefinitionDescriptor d = ResolveDescriptor(sp, descriptor);
                Type runnerType = typeof(ChartRunner<>).MakeGenericType(d.EntityType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));
                object engine = sp.GetRequiredService(
                    typeof(IQueryEngine<>).MakeGenericType(d.EntityType));
                object definition = sp.GetRequiredService(
                    typeof(QueryDefinition<>).MakeGenericType(d.EntityType));

                return (IChartRunner)Activator.CreateInstance(
                    runnerType, d.Name, queryableSource, engine, definition)!;
            });

            services.AddScoped<IPivotRunner>(sp =>
            {
                IQueryDefinitionDescriptor d = ResolveDescriptor(sp, descriptor);
                Type runnerType = typeof(PivotRunner<>).MakeGenericType(d.EntityType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));
                object engine = sp.GetRequiredService(
                    typeof(IQueryEngine<>).MakeGenericType(d.EntityType));
                object definition = sp.GetRequiredService(
                    typeof(QueryDefinition<>).MakeGenericType(d.EntityType));

                return (IPivotRunner)Activator.CreateInstance(
                    runnerType, d.Name, queryableSource, engine, definition)!;
            });

            services.AddScoped<IMapRunner>(sp =>
            {
                IQueryDefinitionDescriptor d = ResolveDescriptor(sp, descriptor);
                Type runnerType = typeof(MapRunner<>).MakeGenericType(d.EntityType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));
                object engine = sp.GetRequiredService(
                    typeof(IQueryEngine<>).MakeGenericType(d.EntityType));
                AnalyticsEndpointsMetrics metrics = sp.GetRequiredService<AnalyticsEndpointsMetrics>();
                ICurrentTenant? currentTenant = sp.GetService<ICurrentTenant>();

                return (IMapRunner)Activator.CreateInstance(
                    runnerType, d.Name, queryableSource, engine, metrics, currentTenant)!;
            });
        }

        services.AddScoped<IWidgetInstanceRenderer, KpiWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, TableWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, ChartWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, PivotWidgetInstanceRenderer>();
        services.AddScoped<IWidgetInstanceRenderer, MapWidgetInstanceRenderer>();

        return services;
    }

    private static IQueryDefinitionDescriptor ResolveDescriptor(IServiceProvider sp, ServiceDescriptor descriptor) =>
        (IQueryDefinitionDescriptor)(descriptor.ImplementationFactory?.Invoke(sp)
            ?? throw new InvalidOperationException(
                "IQueryDefinitionDescriptor must be registered with an implementation factory."));
}
