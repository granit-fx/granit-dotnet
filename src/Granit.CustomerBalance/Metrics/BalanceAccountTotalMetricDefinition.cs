using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.CustomerBalance.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.CustomerBalance.Metrics;

/// <summary>
/// Sum of every balance account's <see cref="BalanceAccount.Balance"/> — total
/// outstanding credit liability the platform owes its customers. Multi-currency
/// tenants carry mixed currencies; the host renders the value with its tenant's
/// configured currency for display.
/// </summary>
public sealed class BalanceAccountTotalMetricDefinition : MetricDefinition<BalanceAccount, decimal>
{
    /// <inheritdoc />
    public override string Name => "Granit.CustomerBalance.BalanceAccountTotalMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Currency;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Sum;

    /// <inheritdoc />
    public override Expression<Func<BalanceAccount, decimal?>>? Selector
        => a => a.Balance;

    /// <inheritdoc />
    public override Expression<Func<BalanceAccount, DateTimeOffset>>? PeriodSelector
        => a => a.CreatedAt;
}
