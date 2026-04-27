using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for a personal data deletion request acknowledgement — sent to the
/// data subject as soon as their deletion request is received, before the cooling-off
/// period or saga starts. Establishes the GDPR Art. 12 §3 paper trail showing that the
/// controller responded "without undue delay".
/// </summary>
/// <remarks>
/// Channel: Email only — this is a compliance record. <see cref="NotificationSeverity.Info"/>
/// because no action is required from the user; the email simply confirms receipt and
/// states the regulatory deadline by which the controller will execute the request.
/// </remarks>
public sealed class PrivacyDeletionAcknowledgedNotificationType
    : NotificationType<PrivacyDeletionAcknowledgedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyDeletionAcknowledgedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.deletion_acknowledged";

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a deletion-acknowledged notification.
/// </summary>
/// <param name="RequestId">The deletion request identifier.</param>
/// <param name="RequestedAt">When the data subject filed the request.</param>
/// <param name="Regulation">Privacy regulation code (e.g. <c>EU_GDPR</c>).</param>
public sealed record PrivacyDeletionAcknowledgedNotificationData(
    Guid RequestId,
    DateTimeOffset RequestedAt,
    string Regulation);
