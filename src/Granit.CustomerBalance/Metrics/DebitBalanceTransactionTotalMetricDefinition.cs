using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.CustomerBalance.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.CustomerBalance.Metrics;

/// <summary>
/// Sum of <see cref="BalanceTransaction.Amount"/> for ledger entries of type
/// <see cref="TransactionType.Debit"/> in the period — the debit-side volume
/// (credits consumed against invoices, manual deductions). Reads as the actual
/// monetary value of credit applied during the window.
/// </summary>
public sealed class DebitBalanceTransactionTotalMetricDefinition : MetricDefinition<BalanceTransaction, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.CustomerBalance.DebitBalanceTransactionTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<BalanceTransaction, decimal?>>? Selector
        => t => t.Amount;

    /// <inheritdoc />
    public override Expression<Func<BalanceTransaction, bool>>? BaseFilter
        => t => t.Type == TransactionType.Debit;

    /// <inheritdoc />
    public override Expression<Func<BalanceTransaction, DateTimeOffset>>? PeriodSelector
        => t => t.CreatedAt;
}
