using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Number of payment transactions in <see cref="PaymentStatus.Processing"/> state —
/// asynchronous captures that the provider has accepted but not yet finalised
/// (typical for SEPA bank transfers, ACH, hosted-page redirects awaiting return).
/// Sustained high counts may indicate stuck reconciliation rather than ongoing
/// activity.
/// </summary>
public sealed class PendingPaymentTransactionCountMetricDefinition : MetricDefinition<PaymentTransaction, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.PendingPaymentTransactionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, bool>>? BaseFilter
        => t => t.Status == PaymentStatus.Processing;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, DateTimeOffset>>? PeriodSelector
        => t => t.CreatedAt;
}
