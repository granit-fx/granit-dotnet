using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// Stub <see cref="IDatasourceEvaluator{TDatasource}"/> for
/// <see cref="QueryAggregateDatasource"/> — returns an "unavailable" evaluation
/// until the real implementation lands. Wired in B3-2 so the polymorphic
/// <see cref="Datasource"/> dispatch in <see cref="KpiWidgetInstanceRenderer"/>
/// covers every shipped <c>Datasource</c> kind, even when the underlying
/// pipeline is not yet built — the renderer never throws on an unknown kind,
/// it surfaces a localized <c>Widget:Unavailable.*</c> reason instead.
/// </summary>
/// <remarks>
/// The full implementation builds an <c>IQueryable&lt;TEntity&gt;</c> from the
/// named <c>QueryDefinition</c>, applies the dashboard filter spec + period
/// selector, then runs the declared <see cref="QueryEngine.Filtering.AggregateFunction"/>.
/// Empty-set semantics mirror the metric path (<c>Sum</c>/<c>Count</c> → 0,
/// <c>Avg</c>/<c>Min</c>/<c>Max</c> → null) — see ADR-039 §7.bis. Tracked as
/// a follow-up slice; until then a dashboard widget bound to a query-aggregate
/// datasource renders as <c>Unavailable</c> rather than falling over.
/// </remarks>
internal sealed class QueryAggregateDatasourceEvaluator : IDatasourceEvaluator<QueryAggregateDatasource>
{
    public Task<KpiEvaluation> EvaluateAsync(
        QueryAggregateDatasource datasource,
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(KpiEvaluation.Unavailable(
            RefreshHint.Static,
            "Widget:Unavailable.QueryAggregateNotImplemented"));
}
