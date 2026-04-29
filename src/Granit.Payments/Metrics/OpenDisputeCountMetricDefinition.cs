using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Number of payment disputes currently in <see cref="DisputeStatus.Open"/> state —
/// the live workload for the chargeback team. Period selector is
/// <see cref="Dispute.CreatedAt"/> so <c>?period=mtd</c> answers "how many disputes
/// were filed this month?". Lower is better.
/// </summary>
public sealed class OpenDisputeCountMetricDefinition : MetricDefinition<Dispute, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.OpenDisputeCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Dispute, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Dispute, bool>>? BaseFilter
        => d => d.Status == DisputeStatus.Open;

    /// <inheritdoc />
    public override Expression<Func<Dispute, DateTimeOffset>>? PeriodSelector
        => d => d.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
