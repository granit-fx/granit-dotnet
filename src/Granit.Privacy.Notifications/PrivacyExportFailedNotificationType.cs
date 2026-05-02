using Granit.Domain.ValueObjects;
using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for a personal data export that completed partially because the
/// saga timed out before every data provider produced a fragment — sent to the data
/// subject so they know the archive is available but incomplete and can decide whether
/// to retry or accept the partial result.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> because the
/// outcome is degraded but not a hard failure — the archive still exists, only some
/// providers are missing.
/// </remarks>
public sealed class PrivacyExportFailedNotificationType
    : NotificationType<PrivacyExportFailedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyExportFailedNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "privacy.export_failed";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for a partial export notification.
/// </summary>
/// <param name="RequestId">Correlation id of the originating export request.</param>
/// <param name="ArchiveBlobReferenceId">Logical blob reference of the partial archive.</param>
/// <param name="MissingProviders">
/// Providers that did not produce a fragment within the saga timeout. Preserved for
/// programmatic consumers (audit log, retry orchestration) — see
/// <paramref name="MissingProvidersDisplay"/> for the template-friendly form.
/// </param>
/// <param name="MissingProvidersDisplay">
/// Comma-separated rendering of <paramref name="MissingProviders"/> for direct
/// inclusion in templates. The notification email channel serializes the data record
/// to JSON and flattens it into a Scriban dictionary; arrays would otherwise round-trip
/// as their JSON string representation, which is not iterable in templates.
/// </param>
/// <param name="RequestedAt">When the data subject filed the request.</param>
/// <param name="Regulation">Privacy regulation code the request was filed under.</param>
public sealed record PrivacyExportFailedNotificationData(
    Guid RequestId,
    BlobReference ArchiveBlobReferenceId,
    IReadOnlyList<string> MissingProviders,
    string MissingProvidersDisplay,
    DateTimeOffset RequestedAt,
    string Regulation)
{
    /// <summary>
    /// String form of <see cref="ArchiveBlobReferenceId"/> exposed for Scriban templates.
    /// </summary>
    public string ArchiveBlobReferenceDisplay => ArchiveBlobReferenceId.Value;
}
