using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// Per-<see cref="Datasource"/> evaluator producing a KPI tile snapshot from
/// the persisted <see cref="WidgetInstance"/> + the per-render
/// <see cref="WidgetRenderContext"/>. ADR-039 §7.bis — the
/// <see cref="KpiWidgetInstanceRenderer"/> dispatches on
/// <c>Datasource.Kind</c> to the matching closed evaluator (one per
/// <c>MetricDatasource</c> / <c>QueryAggregateDatasource</c> /
/// <c>TelemetryDatasource</c>), so adding a new datasource kind is purely
/// additive (new evaluator + DI registration, no central switch).
/// </summary>
/// <typeparam name="TDatasource">
/// The concrete datasource record this evaluator handles. Pinned by the closed
/// type so the dispatch stays reflection-free at runtime.
/// </typeparam>
public interface IDatasourceEvaluator<TDatasource> where TDatasource : Datasource
{
    /// <summary>
    /// Produces a <see cref="KpiEvaluation"/> for the supplied datasource. The
    /// evaluator <b>must not</b> apply per-widget permission filtering — the
    /// <c>IDashboardRenderer</c> applies it uniformly upstream (ADR-039 §3.a).
    /// Implementations should honour <paramref name="cancellationToken"/> on
    /// every async hop they own.
    /// </summary>
    /// <param name="datasource">The persisted datasource — already deserialised by the renderer from <c>WidgetInstance.ConfigJson</c>.</param>
    /// <param name="widget">The persisted widget — surfaces <c>WidgetType</c>, <c>ConfigJson</c>, <c>MetricName</c> / <c>QueryName</c> as relevant to the kind.</param>
    /// <param name="context">Per-render context — see <see cref="WidgetRenderContext"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KpiEvaluation> EvaluateAsync(
        TDatasource datasource,
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken);
}
