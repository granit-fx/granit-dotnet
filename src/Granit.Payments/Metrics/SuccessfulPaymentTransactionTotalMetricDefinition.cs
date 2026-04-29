using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Payments.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Payments.Metrics;

/// <summary>
/// Total monetary amount of payment transactions in
/// <see cref="PaymentStatus.Succeeded"/> state — sum of
/// <see cref="PaymentTransaction.Amount"/> per successful capture. Currency is
/// intentionally not declared at the metric level since transactions across a tenant
/// may carry different ISO 4217 codes; the host renders with its tenant currency.
/// </summary>
public sealed class SuccessfulPaymentTransactionTotalMetricDefinition : MetricDefinition<PaymentTransaction, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.Payments.SuccessfulPaymentTransactionTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, decimal?>>? Selector
        => t => t.Amount;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, bool>>? BaseFilter
        => t => t.Status == PaymentStatus.Succeeded;

    /// <inheritdoc />
    public override Expression<Func<PaymentTransaction, DateTimeOffset>>? PeriodSelector
        => t => t.SucceededAt!.Value;
}
