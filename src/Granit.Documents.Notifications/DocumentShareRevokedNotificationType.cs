using Granit.Documents.Domain;
using Granit.Notifications;

namespace Granit.Documents.Notifications;

/// <summary>
/// Notification raised when a <see cref="DocumentShare"/> is revoked (F10.3). Sent
/// to the previous grantee so they understand why their access disappeared.
/// </summary>
public sealed class DocumentShareRevokedNotificationType
    : NotificationType<DocumentShareRevokedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly DocumentShareRevokedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "documents.share_revoked";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } = [NotificationChannels.Email];
}

/// <summary>Data payload for the <c>documents.share_revoked</c> notification.</summary>
public sealed record DocumentShareRevokedNotificationData(
    Guid ShareId,
    ShareTargetType TargetType,
    Guid? FolderId,
    Guid? DocumentId,
    string TargetName);
