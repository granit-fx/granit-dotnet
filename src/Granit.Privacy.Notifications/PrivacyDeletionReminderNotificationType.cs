using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for deletion reminder — sent N days before the grace period expires.
/// Channels: Email + InApp by default.
/// </summary>
public sealed class PrivacyDeletionReminderNotificationType
    : NotificationType<PrivacyDeletionReminderNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyDeletionReminderNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.deletion_reminder";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a deletion reminder notification.
/// </summary>
/// <param name="RequestId">The deletion request identifier.</param>
/// <param name="ScheduledDeletionAt">When the data will be permanently deleted.</param>
public sealed record PrivacyDeletionReminderNotificationData(
    Guid RequestId,
    DateTimeOffset ScheduledDeletionAt);
