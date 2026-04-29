using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Number of payment transactions in <see cref="PaymentStatus.Failed"/> state — the
/// payment was attempted but rejected by the provider (declined card, expired card,
/// insufficient funds, etc.). High counts in a short window typically indicate
/// either a provider outage or fraud attempts.
/// </summary>
public sealed class FailedPaymentTransactionCountMetricDefinition : MetricDefinition<PaymentTransaction, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.FailedPaymentTransactionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, bool>>? BaseFilter
        => t => t.Status == PaymentStatus.Failed;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, DateTimeOffset>>? PeriodSelector
        => t => t.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
