using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for deletion confirmation — sent after data has been permanently deleted.
/// Channel: Email only by default (important compliance record).
/// </summary>
public sealed class PrivacyDeletionConfirmationNotificationType
    : NotificationType<PrivacyDeletionConfirmationNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyDeletionConfirmationNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.deletion_confirmed";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a deletion confirmation notification.
/// </summary>
/// <param name="RequestId">The deletion request identifier.</param>
/// <param name="ExecutedAt">When the deletion was executed.</param>
public sealed record PrivacyDeletionConfirmationNotificationData(
    Guid RequestId,
    DateTimeOffset ExecutedAt);
