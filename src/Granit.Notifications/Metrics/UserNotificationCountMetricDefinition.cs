using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Notifications.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Notifications.Metrics;

/// <summary>
/// Total number of in-app notifications dispatched in the period — overall
/// throughput of the notification system. Useful paired with
/// <c>UnreadUserNotificationCount</c>: the gap reveals read-rate pressure.
/// </summary>
public sealed class UserNotificationCountMetricDefinition : MetricDefinition<UserNotification, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Notifications.UserNotificationCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, DateTimeOffset>>? PeriodSelector
        => n => n.CreatedAt;
}
