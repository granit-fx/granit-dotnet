using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.CustomerBalance.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.CustomerBalance.Metrics;

/// <summary>
/// Number of balance accounts on file. One per (party × currency) tuple, so this
/// counts both customer coverage breadth and currency spread.
/// </summary>
public sealed class BalanceAccountCountMetricDefinition : MetricDefinition<BalanceAccount, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.CustomerBalance.BalanceAccountCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<BalanceAccount, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<BalanceAccount, DateTimeOffset>>? PeriodSelector
        => a => a.CreatedAt;
}
