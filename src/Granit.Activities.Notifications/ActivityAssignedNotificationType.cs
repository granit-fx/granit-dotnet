using Granit.Notifications;

namespace Granit.Activities.Notifications;

/// <summary>
/// Notification type sent to the assignee when a new activity is created
/// for them (or when an existing activity is reassigned to them — the
/// runtime handler is the same, story A2 raises the
/// <c>ActivityAssignedEvent</c> in both flows).
/// </summary>
public sealed class ActivityAssignedNotificationType
    : NotificationType<ActivityAssignedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ActivityAssignedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "activity.assigned";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Payload for <see cref="ActivityAssignedNotificationType"/>.
/// </summary>
public sealed record ActivityAssignedNotificationData(
    Guid ActivityId,
    string Type,
    DateTimeOffset DueAt,
    string EntityType,
    Guid EntityId);
