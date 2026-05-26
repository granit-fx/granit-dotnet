using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for a personal data export that completed successfully —
/// sent to the data subject after the archive assembly job persisted every shard.
/// Carries the shard count so the email template can render one BFF download
/// link per shard (no direct presigned URLs in the email body — every download
/// stays under the step-up authentication gate).
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
/// <param name="ShardCount">Number of shard ZIPs the assembly job produced.
/// Templates iterate <c>0..(ShardCount - 1)</c> to render one BFF download
/// link per shard via <c>{{ app.base_url }}/privacy/exports/{{ model.request_id }}/download/{{ index }}</c>.</param>
/// <param name="RequestedAt">When the data subject filed the request.</param>
/// <param name="Regulation">Privacy regulation code the request was filed under
/// (e.g. <c>EU_GDPR</c>). Templates pick the localised display form (RGPD / DSGVO
/// / RODO / GDPR) via the surrounding copy, not from this raw code.</param>
public sealed record PrivacyExportReadyNotificationData(
    Guid RequestId,
    int ShardCount,
    DateTimeOffset RequestedAt,
    string Regulation);
