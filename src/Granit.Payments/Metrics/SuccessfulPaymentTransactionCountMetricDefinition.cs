using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Number of payment transactions in <see cref="PaymentStatus.Succeeded"/> state —
/// captures the volume of successful captures over the period. Period selector is
/// <see cref="PaymentTransaction.SucceededAt"/> so <c>?period=last_30d</c> answers
/// "how many payments completed in the last 30 days?".
/// </summary>
public sealed class SuccessfulPaymentTransactionCountMetricDefinition : MetricDefinition<PaymentTransaction, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SuccessfulPaymentTransactionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, bool>>? BaseFilter
        => t => t.Status == PaymentStatus.Succeeded;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, DateTimeOffset>>? PeriodSelector
        => t => t.SucceededAt!.Value;
}
