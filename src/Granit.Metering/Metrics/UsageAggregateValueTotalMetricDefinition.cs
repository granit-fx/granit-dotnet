using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Metering.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Metering.Metrics;

/// <summary>
/// Sum of <see cref="UsageAggregate.AggregatedValue"/> across rollup rows in the
/// period — total metered usage volume (API calls, GB processed, seats hour…
/// the unit varies per <c>MeterDefinition</c>). Reads as the headline
/// usage-volume KPI for the tenant.
/// </summary>
/// <remarks>
/// Mixed-meter dashboards may sum across heterogeneous units (calls + GB);
/// callers wanting unit-isolated aggregates should pass a
/// <c>MeterDefinitionId</c> filter via the QueryEngine pipeline. The metric
/// itself is unit-agnostic — the host renders with the relevant suffix.
/// </remarks>
public sealed class UsageAggregateValueTotalMetricDefinition : MetricDefinition<UsageAggregate, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Metering.UsageAggregateValueTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Number;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<UsageAggregate, decimal?>>? Selector
        => a => a.AggregatedValue;

    /// <inheritdoc />
    public override Expression<Func<UsageAggregate, DateTimeOffset>>? PeriodSelector
        => a => a.PeriodStart;
}
