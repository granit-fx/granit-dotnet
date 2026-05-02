using Granit.Notifications;

namespace Granit.Activities.Notifications;

/// <summary>
/// Notification type sent to the assignee the day before
/// <see cref="Granit.Activities.Domain.Activity.DueAt"/>. Triggered by the
/// reminder background job (story A8) emitting
/// <see cref="Granit.Activities.Events.ActivityReminderDueEvent"/>.
/// </summary>
public sealed class ActivityReminderNotificationType
    : NotificationType<ActivityReminderNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ActivityReminderNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "activity.reminder";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Payload for <see cref="ActivityReminderNotificationType"/>.</summary>
public sealed record ActivityReminderNotificationData(
    Guid ActivityId,
    string Type,
    DateTimeOffset DueAt,
    string EntityType,
    Guid EntityId);
