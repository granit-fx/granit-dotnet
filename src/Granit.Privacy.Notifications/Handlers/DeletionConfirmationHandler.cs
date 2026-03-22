using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="DeletionExecutedEto"/> by sending a confirmation notification
/// after data has been permanently deleted. Triggered in both immediate and deferred paths.
/// </summary>
internal static class DeletionConfirmationHandler
{
    public static async Task HandleAsync(
        DeletionExecutedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            PrivacyDeletionConfirmationNotificationType.Instance,
            new PrivacyDeletionConfirmationNotificationData(evt.RequestId, evt.ExecutedAt),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
