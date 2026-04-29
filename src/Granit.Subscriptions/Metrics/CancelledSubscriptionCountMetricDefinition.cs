using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of subscriptions cancelled in the period. Period selector is
/// <see cref="Subscription.CancelledAt"/> — when called with <c>?period=mtd</c>,
/// counts cancellations that happened during the current month-to-date.
/// </summary>
/// <remarks>
/// Distinct from <see cref="SubscriptionStatus.Cancelled"/> count — the cancelled
/// status is terminal (no further state change), so the all-time count grows
/// monotonically. The period-bounded view answers "how many churned this month?".
/// </remarks>
public sealed class CancelledSubscriptionCountMetricDefinition : MetricDefinition<Subscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.CancelledSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Subscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.CancelledAt != null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CancelledAt!.Value;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
