using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Notifications.Abstractions;

namespace Granit.Documents.Notifications.Handlers;

/// <summary>
/// Subscribes to <see cref="DocumentShareGrantedEvent"/> and publishes the
/// <c>documents.shared</c> notification to the grantee. Only fires for
/// <see cref="ShareGranteeType.User"/> grants — role / group fan-out is the host's
/// responsibility (the recipient list depends on the role/group resolution layer
/// which is not in scope for the framework).
/// </summary>
public sealed class DocumentSharedHandler
{
    public static async Task HandleAsync(
        DocumentShareGrantedEvent evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (evt.GranteeType != ShareGranteeType.User)
        {
            return;
        }

        await publisher.PublishAsync(
            DocumentSharedNotificationType.Instance,
            new DocumentSharedNotificationData(
                evt.ShareId,
                evt.TargetType,
                evt.FolderId,
                evt.DocumentId,
                TargetName: string.Empty,
                evt.Permission,
                evt.ExpiresAt),
            [evt.GranteeId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
