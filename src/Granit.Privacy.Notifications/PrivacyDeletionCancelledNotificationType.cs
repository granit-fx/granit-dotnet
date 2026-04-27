using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for a deletion request that was cancelled during the cooling-off
/// period — sent to the data subject when they revoke their pending deletion request.
/// Important compliance trail: documents that the user actively un-asked for the deletion.
/// </summary>
/// <remarks>
/// Channel: Email only (compliance record). <see cref="NotificationSeverity.Info"/>.
/// </remarks>
public sealed class PrivacyDeletionCancelledNotificationType
    : NotificationType<PrivacyDeletionCancelledNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyDeletionCancelledNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.deletion_cancelled";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a deletion-cancelled notification.
/// </summary>
/// <param name="RequestId">The deletion request identifier.</param>
/// <param name="CancelledAt">When the cancellation was recorded.</param>
public sealed record PrivacyDeletionCancelledNotificationData(
    Guid RequestId,
    DateTimeOffset CancelledAt);
