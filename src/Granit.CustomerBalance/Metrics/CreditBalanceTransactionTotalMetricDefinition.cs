using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.CustomerBalance.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.CustomerBalance.Metrics;

/// <summary>
/// Sum of <see cref="BalanceTransaction.Amount"/> for ledger entries of type
/// <see cref="TransactionType.Credit"/> in the period — the credit-side volume
/// (promotional grants, manual adjustments, overpayment surplus) that flowed into
/// customer balances.
/// </summary>
public sealed class CreditBalanceTransactionTotalMetricDefinition : MetricDefinition<BalanceTransaction, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.CustomerBalance.CreditBalanceTransactionTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<BalanceTransaction, decimal?>>? Selector
        => t => t.Amount;

    /// <inheritdoc />
    public override Expression<Func<BalanceTransaction, bool>>? BaseFilter
        => t => t.Type == TransactionType.Credit;

    /// <inheritdoc />
    public override Expression<Func<BalanceTransaction, DateTimeOffset>>? PeriodSelector
        => t => t.CreatedAt;
}
