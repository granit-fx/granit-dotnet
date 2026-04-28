using Granit.Analytics.Extensions;
using Granit.Dashboards;
using Granit.Modularity;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Analytics;

/// <summary>
/// Granit module for declarative analytics primitives — <see cref="Metrics.MetricDefinition{TEntity, TValue}"/>
/// for inline KPIs, <see cref="Widgets.IWidgetSource{TPayload}"/> for the (pull / future-push) widget contract.
/// </summary>
/// <remarks>
/// <para>
/// Definitions are pure declarations and do not require a runtime — only hosts that execute
/// metrics need <c>Granit.Analytics.EntityFrameworkCore</c> (the EF Core executor) and
/// <c>Granit.Analytics.Endpoints</c> (the HTTP surface).
/// </para>
/// <para>
/// The module ships a <c>Granit.Analytics</c> meter (<see cref="Diagnostics.AnalyticsMetrics"/>)
/// and an ActivitySource (<see cref="Diagnostics.AnalyticsActivitySource"/>); both are registered
/// with <c>GranitActivitySourceRegistry</c> by <see cref="AnalyticsServiceCollectionExtensions.AddGranitAnalytics"/>
/// so OpenTelemetry pipelines pick them up automatically.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitDashboardsAbstractionsModule),
    typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitAnalyticsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitAnalytics();
}
