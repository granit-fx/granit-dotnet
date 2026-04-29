using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Number of refunds in <see cref="RefundStatus.Succeeded"/> state — captures the
/// volume of completed money-back operations. Period selector is
/// <see cref="Refund.CompletedAt"/> so a rolling 30-day window answers "how many
/// refunds we honoured this month?". Lower is better — refunds are a cost.
/// </summary>
public sealed class SuccessfulRefundCountMetricDefinition : MetricDefinition<Refund, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SuccessfulRefundCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Refund, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Refund, bool>>? BaseFilter
        => r => r.Status == RefundStatus.Succeeded;

    /// <inheritdoc />
    public override Expression<Func<Refund, DateTimeOffset>>? PeriodSelector
        => r => r.CompletedAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
