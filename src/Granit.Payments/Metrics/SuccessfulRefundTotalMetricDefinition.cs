using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Total monetary amount refunded successfully in the period — sum of
/// <see cref="Refund.Amount"/> across refunds in <see cref="RefundStatus.Succeeded"/>.
/// Lower is better; reconciles with the negative side of net revenue.
/// </summary>
public sealed class SuccessfulRefundTotalMetricDefinition : MetricDefinition<Refund, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SuccessfulRefundTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<Refund, decimal?>>? Selector
        => r => r.Amount;

    /// <inheritdoc />
    public override Expression<Func<Refund, bool>>? BaseFilter
        => r => r.Status == RefundStatus.Succeeded;

    /// <inheritdoc />
    public override Expression<Func<Refund, DateTimeOffset>>? PeriodSelector
        => r => r.CompletedAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
