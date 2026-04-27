using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="DeletionDeferredEto"/> by confirming the new deletion deadline
/// to the data subject. Distinct from the J-N reminder — this email fires immediately
/// when the user opts to defer, while <c>privacy.deletion_reminder</c> fires later as
/// the deadline approaches.
/// </summary>
public class DeletionDeferredConfirmedHandler
{
    public static async Task HandleAsync(
        DeletionDeferredEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            PrivacyDeletionDeferredConfirmedNotificationType.Instance,
            new PrivacyDeletionDeferredConfirmedNotificationData(
                evt.RequestId,
                evt.RequestedAt,
                evt.ScheduledDeletionAt,
                evt.Regulation),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
