using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="DeletionCancelledEto"/> by confirming to the data subject that
/// their deferred deletion request has been revoked and their data will be retained.
/// </summary>
public class DeletionCancelledHandler
{
    public static async Task HandleAsync(
        DeletionCancelledEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            PrivacyDeletionCancelledNotificationType.Instance,
            new PrivacyDeletionCancelledNotificationData(
                evt.RequestId,
                evt.CancelledAt),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
