using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of subscriptions currently in dunning — at least one failed payment
/// retry attempt has occurred. Tracks the active recovery workload separately
/// from <c>PastDueSubscriptionCount</c> (status-based) since dunning may begin
/// before the status flips.
/// </summary>
public sealed class DunningSubscriptionCountMetricDefinition : MetricDefinition<Subscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.DunningSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Subscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.DunningAttempt > 0;

    /// <inheritdoc />
    public override Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
