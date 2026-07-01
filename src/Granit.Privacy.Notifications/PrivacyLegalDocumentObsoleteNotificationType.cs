using Granit.Notifications;

namespace Granit.Privacy.Notifications;

/// <summary>
/// Notification type for legal document version obsolescence — sent to users who need to
/// re-consent after a new version of a legal document is published.
/// </summary>
public sealed class PrivacyLegalDocumentObsoleteNotificationType
    : NotificationType<PrivacyLegalDocumentObsoleteNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly PrivacyLegalDocumentObsoleteNotificationType Instance = new();

    /// <inheritdoc />
    // Breaking rename from legacy "Privacy.LegalDocumentObsolete": hosts with stored user
    // notification-subscription preferences keyed by the old PascalCase name need a host-specific data migration.
    public override string Name => "privacy.legal_document_obsolete";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email];
}

/// <summary>
/// Data payload for a legal document obsolescence notification.
/// </summary>
/// <param name="DocumentId">The legal document identifier (e.g., <c>"privacy-policy"</c>).</param>
/// <param name="DocumentDisplayName">Human-readable document title.</param>
/// <param name="OldVersion">The version the user previously accepted.</param>
/// <param name="NewVersion">The new version that requires re-consent.</param>
public sealed record PrivacyLegalDocumentObsoleteNotificationData(
    string DocumentId,
    string DocumentDisplayName,
    string OldVersion,
    string NewVersion);
