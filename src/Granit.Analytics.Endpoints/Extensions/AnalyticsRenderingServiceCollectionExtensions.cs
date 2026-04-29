using Granit.Analytics.Endpoints.Rendering;
using Granit.Analytics.EntityFrameworkCore.Extensions;
using Granit.Analytics.Rendering;
using Granit.Dashboards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Analytics.Endpoints.Extensions;

/// <summary>
/// DI registration helper for the HTTP-side rendering pipeline. The renderers,
/// snapshots and non-HTTP datasource evaluators (QueryAggregate, Telemetry) live in
/// <c>Granit.Analytics.Rendering</c> and are auto-wired by <c>AddGranitAnalytics()</c>;
/// this method only adds the HTTP-coupled <see cref="MetricDatasourceEvaluator"/>
/// (which depends on the FusionCache-backed <c>MetricEndpointService</c>) plus the
/// EF Core runner registry.
/// </summary>
public static class AnalyticsRenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HTTP-bound metric datasource evaluator and delegates to
    /// <see cref="AnalyticsEntityFrameworkCoreServiceCollectionExtensions.AddGranitAnalyticsRunners"/>
    /// for the EF Core runner factories.
    /// </summary>
    /// <remarks>
    /// MUST be called AFTER every <c>AddQueryDefinition&lt;TEntity, TDef&gt;()</c>
    /// registration — the runner factory loop iterates the descriptors collected at
    /// the time of the call.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitAnalyticsWidgetRenderers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitAnalyticsRunners();

        services.TryAddScoped<IDatasourceEvaluator<MetricDatasource>, MetricDatasourceEvaluator>();

        return services;
    }
}
