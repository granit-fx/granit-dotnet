using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Documents.Notifications.Handlers;

/// <summary>
/// Subscribes to <see cref="DocumentShareRevokedEvent"/> and publishes the
/// <c>documents.share_revoked</c> notification to the previous grantee. Only fires
/// for <see cref="ShareGranteeType.User"/> grants — see
/// <see cref="DocumentSharedHandler"/> for the role/group rationale.
/// </summary>
public sealed class DocumentShareRevokedHandler
{
    public static async Task HandleAsync(
        DocumentShareRevokedEvent evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (evt.GranteeType != ShareGranteeType.User)
        {
            return;
        }

        await publisher.PublishAsync(
            DocumentShareRevokedNotificationType.Instance,
            new DocumentShareRevokedNotificationData(
                evt.ShareId,
                evt.TargetType,
                evt.FolderId,
                evt.DocumentId,
                TargetName: string.Empty),
            [evt.GranteeId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
