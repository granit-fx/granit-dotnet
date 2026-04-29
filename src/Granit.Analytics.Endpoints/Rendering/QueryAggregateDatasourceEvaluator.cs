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
/// <see cref="QueryEngine.IQueryableSource{TEntity}"/> via the pre-registered
/// <see cref="IQueryAggregateRunner"/> registry. All five aggregations
/// (Count / Sum / Avg / Min / Max) are wired (B3-2bis + B3-2ter); the dashboard
/// "open invoices" tile, "average ticket size" tile and "max latency" tile
/// share the same <c>QueryDefinition</c> their admin grids do.
/// </summary>
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

        // Configuration errors (unknown field, unsupported type) propagate and
        // are caught by IDashboardRenderer's per-widget error isolation
        // (ADR-039 §3.c). The dashboard render still returns 200; the
        // misconfigured widget alone is surfaced as Error.
        QueryAggregateRunnerResult result = await runner
            .ExecuteAsync(datasource.Aggregation, datasource.Field, context.DashboardFilters, cancellationToken)
            .ConfigureAwait(false);

        // Empty-set semantics (locked by tests #1374):
        // - Count / Sum: 0 (never null) — NoData stays false.
        // - Avg / Min / Max: null when no rows — NoData true so the frontend
        //   renders the "—" placeholder instead of "0" (which would imply a
        //   real measurement).
        bool noData = !result.Value.HasValue;

        // ValueKind precedence:
        //  1. Count -> Count (always; Count never carries a currency)
        //  2. Currency code declared on the column -> Currency
        //  3. Otherwise -> Number
        MetricValueKind valueKind = datasource.Aggregation switch
        {
            AggregateFunction.Count => MetricValueKind.Count,
            _ when result.CurrencyCode is not null => MetricValueKind.Currency,
            _ => MetricValueKind.Number,
        };

        MetricSnapshotPayload payload = new(
            result.Value,
            ValueKind: valueKind,
            Currency: result.CurrencyCode,
            IsHigherBetter: true,
            NoData: noData,
            Previous: null);

        return KpiEvaluation.Snapshot(payload, RefreshHint.Dynamic);
    }
}
