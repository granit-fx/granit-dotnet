using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Metering.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Metering.Metrics;

/// <summary>
/// Number of usage-aggregate rollup rows produced in the period — proxies the
/// "metering activity" dimension. One rollup row per (meter × tenant × period
/// bucket); a high count signals broad metering coverage, a sudden drop usually
/// signals an upstream outage in the watermark-based aggregator.
/// </summary>
public sealed class UsageAggregateCountMetricDefinition : MetricDefinition<UsageAggregate, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Metering.UsageAggregateCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<UsageAggregate, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<UsageAggregate, DateTimeOffset>>? PeriodSelector
        => a => a.PeriodStart;
}
