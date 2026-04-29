using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of subscriptions currently in <see cref="SubscriptionStatus.Active"/>.
/// The headline SaaS health KPI — directly tracks paying-customer count.
/// </summary>
public sealed class ActiveSubscriptionCountMetricDefinition : MetricDefinition<Subscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.ActiveSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Subscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.Status == SubscriptionStatus.Active;

    /// <inheritdoc />
    public override Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;
}
