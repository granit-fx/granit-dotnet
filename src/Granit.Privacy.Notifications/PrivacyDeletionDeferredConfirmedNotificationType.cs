using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for a deletion request that was deferred during the cooling-off
/// period — sent immediately after the user opts to delay deletion, confirming the new
/// scheduled deletion date. Distinct from <see cref="PrivacyDeletionReminderNotificationType"/>,
/// which fires N days before the scheduled date as a reminder.
/// </summary>
/// <remarks>
/// Channel: Email only (compliance record). <see cref="NotificationSeverity.Info"/>.
/// </remarks>
public sealed class PrivacyDeletionDeferredConfirmedNotificationType
    : NotificationType<PrivacyDeletionDeferredConfirmedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyDeletionDeferredConfirmedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.deletion_deferred_confirmed";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a deletion-deferred-confirmed notification.
/// </summary>
/// <param name="RequestId">The deletion request identifier.</param>
/// <param name="RequestedAt">When the data subject filed the deferred request.</param>
/// <param name="ScheduledDeletionAt">When the data will be permanently deleted.</param>
/// <param name="Regulation">Privacy regulation code (e.g. <c>EU_GDPR</c>).</param>
public sealed record PrivacyDeletionDeferredConfirmedNotificationData(
    Guid RequestId,
    DateTimeOffset RequestedAt,
    DateTimeOffset ScheduledDeletionAt,
    string Regulation);
