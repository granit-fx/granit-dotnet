using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Diagnostics;
using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering the EF Core executor for Granit.Analytics.
/// </summary>
public static class AnalyticsEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the open generic <see cref="MetricExecutor{TEntity, TValue}"/> as scoped.
    /// Combined with the abstractions module's <c>AddGranitAnalytics</c>, this lets any
    /// host resolve <c>IMetricExecutor&lt;TEntity, TValue&gt;</c> for any pair declared
    /// via <c>AddMetricDefinition&lt;TEntity, TValue, TDefinition&gt;</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsEntityFrameworkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(typeof(IMetricExecutor<,>), typeof(MetricExecutor<,>));

        return services;
    }

    /// <summary>
    /// Registers the EF Core-backed widget runner implementations
    /// (<see cref="QueryAggregateRunner{TEntity}"/>, <see cref="TableRunner{TEntity}"/>,
    /// <see cref="ChartRunner{TEntity}"/>, <see cref="PivotRunner{TEntity}"/>,
    /// <see cref="MapRunner{TEntity}"/>) — one closed-generic per registered
    /// <see cref="IQueryDefinitionDescriptor"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MUST be called AFTER every <c>AddQueryDefinition&lt;TEntity, TDef&gt;()</c>
    /// registration — the loop iterates the descriptor service descriptors collected
    /// in the container at the time of this call. Adding more query definitions after
    /// this point silently leaves them unwired.
    /// </para>
    /// <para>
    /// The orchestration registries (<c>QueryAggregateService</c>, <c>TableService</c>,
    /// …) and <see cref="AnalyticsRuntimeMetrics"/> are registered separately by
    /// <c>AddGranitAnalytics()</c> in the main <c>Granit.Analytics</c> module — they are
    /// pure orchestration and do not depend on EF Core, so non-data-layer hosts get
    /// them automatically.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsRunners(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // One closed-generic MetricRunner<TEntity, TValue> per registered metric definition.
        // The runner wraps IMetricExecutor<,> (this package) so request-time dispatch from
        // MetricEndpointService stays reflection-free.
        foreach (ServiceDescriptor metricDescriptor in services
            .Where(d => d.ServiceType == typeof(IMetricDefinitionDescriptor))
            .ToList())
        {
            services.AddSingleton<IMetricRunner>(sp =>
            {
                var d = (IMetricDefinitionDescriptor)
                    (metricDescriptor.ImplementationFactory?.Invoke(sp)
                     ?? throw new InvalidOperationException(
                         "IMetricDefinitionDescriptor must be registered with an implementation factory."));

                Type runnerType = typeof(MetricRunner<,>).MakeGenericType(d.EntityType, d.ValueType);
                Type definitionType = typeof(MetricDefinition<,>).MakeGenericType(d.EntityType, d.ValueType);

                object definition = sp.GetRequiredService(definitionType);
                object queryableSource = sp.GetRequiredService(
                    typeof(IQueryableSource<>).MakeGenericType(d.EntityType));
                object executor = sp.GetRequiredService(
                    typeof(IMetricExecutor<,>).MakeGenericType(d.EntityType, d.ValueType));

                return (IMetricRunner)Activator.CreateInstance(runnerType, definition, queryableSource, executor)!;
            });
        }

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

                return (IQueryAggregateRunner)Activator.CreateInstance(
                    runnerType, d.Name, queryableSource, engine, definition)!;
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
                AnalyticsRuntimeMetrics metrics = sp.GetRequiredService<AnalyticsRuntimeMetrics>();
                ICurrentTenant? currentTenant = sp.GetService<ICurrentTenant>();
                // Optional Geography projector — registered by Granit.Analytics.PostGIS
                // (or any other geography provider) per entity type. Absent on hosts
                // that don't pull a provider; runner falls back to LatLng-only.
                Type projectorType = typeof(IGeographyPointProjector<>).MakeGenericType(d.EntityType);
                object? geographyProjector = sp.GetService(projectorType);

                return (IMapRunner)Activator.CreateInstance(
                    runnerType, d.Name, queryableSource, engine, metrics, currentTenant, geographyProjector)!;
            });
        }

        return services;
    }

    private static IQueryDefinitionDescriptor ResolveDescriptor(IServiceProvider sp, ServiceDescriptor descriptor) =>
        (IQueryDefinitionDescriptor)(descriptor.ImplementationFactory?.Invoke(sp)
            ?? throw new InvalidOperationException(
                "IQueryDefinitionDescriptor must be registered with an implementation factory."));
}
