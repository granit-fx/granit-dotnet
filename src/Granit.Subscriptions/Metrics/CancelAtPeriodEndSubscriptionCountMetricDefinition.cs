using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of subscriptions flagged to cancel at the end of the current billing
/// period — leading indicator of upcoming churn. Useful to anticipate next-period
/// MRR loss before the cancellation actually fires.
/// </summary>
public sealed class CancelAtPeriodEndSubscriptionCountMetricDefinition : MetricDefinition<Subscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.CancelAtPeriodEndSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Subscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.CancelAtPeriodEnd && s.Status == SubscriptionStatus.Active;

    /// <inheritdoc />
    public override Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
