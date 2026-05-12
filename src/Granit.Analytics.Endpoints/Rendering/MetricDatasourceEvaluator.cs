using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Analytics.Rendering;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;

namespace Granit.Analytics.Endpoints.Rendering;

/// <summary>
/// <see cref="IDatasourceEvaluator{TDatasource}"/> for <see cref="MetricDatasource"/> —
/// resolves the matching <see cref="IMetricRunner"/> by name, runs it against
/// the resolved render-context period, and shapes the result into a
/// <see cref="MetricSnapshotPayload"/>. Reuses the same runner registry as the
/// inline <c>POST /metrics/{name}</c> path so dashboard-rendered KPIs and ad-hoc
/// metric requests cannot diverge in semantics.
/// </summary>
/// <remarks>
/// <para>
/// The cache layer used by <see cref="MetricEndpointService"/> is intentionally
/// bypassed here: the dashboard render endpoint owns its own cache key recipe
/// (ADR-039 §5 — the same key seeds the future WS subscription topic). Wiring
/// FusionCache once at the renderer level avoids two caches stamping each
/// other's TTL. The metric-runner pipeline (BaseFilter, period selector,
/// multi-tenant filter) still applies through <see cref="IMetricRunner.ExecuteAsync"/>.
/// </para>
/// <para>
/// Comparison-window deltas (<c>compareTo</c>) are not surfaced by the
/// dashboard render endpoint in B3-2 — KPI tiles render the bare value plus
/// any client-side delta widget. When a future story adds dashboard-level
/// comparison the evaluator will populate <see cref="MetricSnapshotPayload.Previous"/>.
/// </para>
/// </remarks>
internal sealed class MetricDatasourceEvaluator(MetricEndpointService metricService)
    : IDatasourceEvaluator<MetricDatasource>
{
    private readonly MetricEndpointService _metricService = metricService;

    public async Task<KpiEvaluation> EvaluateAsync(
        MetricDatasource datasource,
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datasource);
        ArgumentNullException.ThrowIfNull(widget);
        ArgumentNullException.ThrowIfNull(context);

        if (!_metricService.TryGetRunner(datasource.MetricName, out IMetricRunner runner))
        {
            return KpiEvaluation.Unavailable(RefreshHint.Static, "Widget:Unavailable.MetricNotFound");
        }

        ResolvedPeriod? period = runner.SupportsPeriod ? context.Period : null;

        decimal? value = await runner
            .ExecuteAsync(period, context.DashboardFilters, cancellationToken)
            .ConfigureAwait(false);

        MetricSnapshotPayload payload = new(
            value,
            runner.ValueKind,
            runner.CurrencyCode,
            runner.IsHigherBetter,
            NoData: !value.HasValue,
            Previous: null);

        return KpiEvaluation.Snapshot(payload, runner.RefreshHint);
    }
}
