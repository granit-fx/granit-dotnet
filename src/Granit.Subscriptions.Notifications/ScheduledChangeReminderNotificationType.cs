using Granit.Notifications;

namespace Granit.Subscriptions.Notifications;

/// <summary>Sent to remind the subscriber of an upcoming scheduled plan change.</summary>
public sealed class ScheduledChangeReminderNotificationType
    : NotificationType<ScheduledChangeReminderNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly ScheduledChangeReminderNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "Subscriptions.ScheduledChangeReminder";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>Data for the scheduled change reminder notification.</summary>
public sealed record ScheduledChangeReminderNotificationData(
    Guid SubscriptionId,
    Guid CurrentPlanId,
    string CurrentPlanName,
    Guid NewPlanId,
    string NewPlanName,
    DateTimeOffset ScheduledAt);
