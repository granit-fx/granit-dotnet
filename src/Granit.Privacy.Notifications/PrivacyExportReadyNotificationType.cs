using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for a personal data export that completed successfully —
/// sent to the data subject after the export saga finished without timeouts.
/// Carries the blob reference of the assembled archive so the email template can
/// render a pre-signed download link.
/// </summary>
/// <remarks>
/// Channel: Email only by default — the archive must reach the user as a transactional
/// record. Use <c>NotificationSeverity.Success</c> so in-app rendering surfaces it
/// appropriately if the host extends <see cref="DefaultChannels"/>.
/// </remarks>
public sealed class PrivacyExportReadyNotificationType
    : NotificationType<PrivacyExportReadyNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyExportReadyNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.export_ready";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Success;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for an export-ready notification.
/// </summary>
/// <param name="RequestId">Correlation id of the originating export request.</param>
/// <param name="ArchiveBlobReferenceId">
/// Logical blob reference of the assembled archive — the email template resolves
/// this to a pre-signed download URL via the BlobStorage download endpoint.
/// </param>
/// <param name="RequestedAt">When the data subject filed the request.</param>
/// <param name="Regulation">Privacy regulation code the request was filed under (e.g. <c>EU_GDPR</c>).</param>
public sealed record PrivacyExportReadyNotificationData(
    Guid RequestId,
    string ArchiveBlobReferenceId,
    DateTimeOffset RequestedAt,
    string Regulation);
