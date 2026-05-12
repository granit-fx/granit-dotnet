using System.Linq.Expressions;
using Granit.Analytics.Metrics;
using Granit.Notifications.Domain;
using Granit.QueryEngine.Filtering;

namespace Granit.Notifications.Analytics.Metrics;

/// <summary>
/// Number of in-app notifications still in <see cref="UserNotificationState.Unread"/>
/// — the inbox backlog. Period selector is <see cref="UserNotification.CreatedAt"/>
/// so a windowed view answers "how much unread piled up this month?".
/// </summary>
public sealed class UnreadUserNotificationCountMetricDefinition : MetricDefinition<UserNotification, int>
{
    /// <inheritdoc />
    public override string Name => "Granit.Notifications.UnreadUserNotificationCountMetric";

    /// <inheritdoc />
    public override MetricValueKind ValueKind => MetricValueKind.Count;

    /// <inheritdoc />
    public override AggregateFunction Aggregation => AggregateFunction.Count;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, int?>>? Selector => null;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, bool>>? BaseFilter
        => n => n.State == UserNotificationState.Unread;

    /// <inheritdoc />
    public override Expression<Func<UserNotification, DateTimeOffset>>? PeriodSelector
        => n => n.CreatedAt;

    /// <inheritdoc />
    public override bool IsHigherBetter => false;
}
