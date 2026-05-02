using Granit.Notifications;

namespace Granit.Activities.Notifications;

/// <summary>
/// Notification type sent to the assignee the first time the overdue
/// background job (story A8) observes an activity past its due date.
/// Idempotency is tracked on the activity row (<c>OverdueNotifiedAt</c>) so
/// the job never re-fires this notification for the same activity within a
/// polling window.
/// </summary>
public sealed class ActivityOverdueNotificationType
    : NotificationType<ActivityOverdueNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ActivityOverdueNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "activity.overdue";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Payload for <see cref="ActivityOverdueNotificationType"/>.
/// </summary>
public sealed record ActivityOverdueNotificationData(
    Guid ActivityId,
    string Type,
    DateTimeOffset DueAt,
    int OverdueByDays,
    string EntityType,
    Guid EntityId);
