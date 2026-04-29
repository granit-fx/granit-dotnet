using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of subscriptions currently in <see cref="SubscriptionStatus.PastDue"/> —
/// payment failed at least once and the subscription is in its grace period before
/// suspension. Operational signal: high values warrant intervention from billing ops.
/// </summary>
public sealed class PastDueSubscriptionCountMetricDefinition : MetricDefinition<Subscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.PastDueSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Subscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.Status == SubscriptionStatus.PastDue;

    /// <inheritdoc />
    public override Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
