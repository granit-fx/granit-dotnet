using Granit.Documents.Domain;
using Granit.Notifications;

namespace Granit.Documents.Notifications;

/// <summary>
/// Notification raised when a <see cref="DocumentShare"/> is granted on a folder or
/// a document (F10.2). Sent to the grantee so they discover the new share without
/// polling the share list.
/// </summary>
public sealed class DocumentSharedNotificationType
    : NotificationType<DocumentSharedNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly DocumentSharedNotificationType Instance = new();

    /// <inheritdoc/>
    public override string Name => "documents.shared";

    /// <inheritdoc/>
    public override IReadOnlyList<string> DefaultChannels { get; } = [NotificationChannels.Email];
}

/// <summary>Data payload for the <c>documents.shared</c> notification.</summary>
/// <param name="ShareId">Identifier of the <c>DocumentShare</c> row.</param>
/// <param name="TargetType">Whether the grant lives on a folder or a document.</param>
/// <param name="FolderId">Folder identifier when <see cref="TargetType"/> is <c>Folder</c>.</param>
/// <param name="DocumentId">Document identifier when <see cref="TargetType"/> is <c>Document</c>.</param>
/// <param name="TargetName">Folder or document name (for templated rendering).</param>
/// <param name="Permission">Permission level the grantee received.</param>
/// <param name="ExpiresAt">Optional UTC instant the grant expires; <c>null</c> means open-ended.</param>
public sealed record DocumentSharedNotificationData(
    Guid ShareId,
    ShareTargetType TargetType,
    Guid? FolderId,
    Guid? DocumentId,
    string TargetName,
    SharePermissionLevel Permission,
    DateTimeOffset? ExpiresAt);
