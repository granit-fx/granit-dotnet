using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;

namespace Granit.Analytics.Rendering;

/// <summary>
/// Stub <see cref="IDatasourceEvaluator{TDatasource}"/> for
/// <see cref="TelemetryDatasource"/> — returns an "unavailable" evaluation. The
/// real implementation lives in <c>Granit.IoT.Dashboards</c> (deferred, outside
/// <c>granit-dotnet</c>): it resolves <see cref="TelemetryDatasource.EntityAlias"/>
/// against <see cref="WidgetRenderContext.ResolvedEntityAliases"/> and queries
/// the IoT telemetry store. Until that package ships, telemetry-bound KPIs
/// render as <c>Unavailable</c> with a dedicated reason key — same dispatch
/// path, no central <c>switch</c> to amend.
/// </summary>
internal sealed class TelemetryDatasourceEvaluator : IDatasourceEvaluator<TelemetryDatasource>
{
    public Task<KpiEvaluation> EvaluateAsync(
        TelemetryDatasource datasource,
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(KpiEvaluation.Unavailable(
            RefreshHint.Static,
            "Widget:Unavailable.TelemetryNotImplemented"));
}
