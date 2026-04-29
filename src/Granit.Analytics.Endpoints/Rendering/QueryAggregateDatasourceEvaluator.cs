using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// <see cref="IDatasourceEvaluator{TDatasource}"/> for
/// <see cref="QueryAggregateDatasource"/> — runs the declared
/// <see cref="AggregateFunction"/> against the entity's
/// <see cref="QueryEngine.IQueryableSource{TEntity}"/> via the
/// pre-registered <see cref="IQueryAggregateRunner"/> registry.
/// </summary>
/// <remarks>
/// <para>
/// Ships with B3-2bis: <see cref="AggregateFunction.Count"/> is fully wired —
/// the dashboard's "open invoices" / "active customers" / "scheduled jobs"
/// KPI tiles render against the same <c>QueryDefinition</c> that powers the
/// admin grid (one source of truth for "what counts as open"). Empty-set
/// semantics match the metric path: Count over zero rows returns <c>0</c>,
/// never null.
/// </para>
/// <para>
/// Sum / Avg / Min / Max need a typed selector built from the runtime field
/// name plus per-primitive-type dispatch (mirrors
/// <c>MetricExecutor&lt;TEntity, TValue&gt;</c>); they ship in a follow-up
/// slice. Until then those operations surface as Unavailable with a
/// dedicated reason key, so the dashboard renders with a typed unavailable
/// widget instead of falling over.
/// </para>
/// </remarks>
internal sealed class QueryAggregateDatasourceEvaluator(QueryAggregateService queryAggregateService)
    : IDatasourceEvaluator<QueryAggregateDatasource>
{
    private readonly QueryAggregateService _queryAggregateService = queryAggregateService;

    public async Task<KpiEvaluation> EvaluateAsync(
        QueryAggregateDatasource datasource,
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datasource);
        ArgumentNullException.ThrowIfNull(widget);

        if (!_queryAggregateService.TryGetRunner(datasource.QueryName, out IQueryAggregateRunner runner))
        {
            return KpiEvaluation.Unavailable(
                RefreshHint.Static,
                "Widget:Unavailable.QueryAggregateNotFound");
        }

        decimal? value = await runner
            .ExecuteAsync(datasource.Aggregation, datasource.Field, cancellationToken)
            .ConfigureAwait(false);

        if (!value.HasValue)
        {
            // Today: Sum / Avg / Min / Max return null because the runner doesn't
            // implement them yet (B3-2bis ships Count only). Surface a dedicated
            // reason so the frontend can distinguish "not implemented" from
            // "metric not registered" — same widget, different remediation.
            return KpiEvaluation.Unavailable(
                RefreshHint.Static,
                "Widget:Unavailable.QueryAggregateOperationNotImplemented");
        }

        MetricSnapshotPayload payload = new(
            value,
            ValueKind: MetricValueKind.Count,
            Currency: null,
            IsHigherBetter: true,
            NoData: false,
            Previous: null);

        return KpiEvaluation.Snapshot(payload, RefreshHint.Dynamic);
    }
}
