using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine.Filtering;
using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Metrics;

/// <summary>
/// Number of subscriptions currently in <see cref="SubscriptionStatus.Trial"/>.
/// Pipeline indicator — large trial cohort foreshadows revenue (or churn) ahead.
/// </summary>
public sealed class TrialSubscriptionCountMetricDefinition : MetricDefinition<Subscription, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Subscriptions.TrialSubscriptionCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<Subscription, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<Subscription, bool>>? BaseFilter
        => s => s.Status == SubscriptionStatus.Trial;

    /// <inheritdoc />
    public override Expression<Func<Subscription, DateTimeOffset>>? PeriodSelector
        => s => s.CreatedAt;
}
