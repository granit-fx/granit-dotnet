using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Notifications.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Notifications.Metrics;

/// <summary>
/// Number of notifications acknowledged in the period — period selector is
/// <see cref="UserNotification.ReadAt"/> so <c>?period=last_7d</c> answers "how
/// many notifications did users actually read this week?". Together with
/// <c>UserNotificationCount</c> over the same window, gives an effective read
/// rate without needing a server-side rate metric.
/// </summary>
public sealed class ReadUserNotificationCountMetricDefinition : MetricDefinition<UserNotification, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Notifications.ReadUserNotificationCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, bool>>? BaseFilter
        => n => n.State == UserNotificationState.Read;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, DateTimeOffset>>? PeriodSelector
        => n => n.ReadAt!.Value;
}
