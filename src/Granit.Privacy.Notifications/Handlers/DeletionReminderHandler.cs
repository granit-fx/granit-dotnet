using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="DeletionReminderDueEto"/> by sending a reminder notification
/// to the user before the deletion deadline.
/// </summary>
internal static partial class DeletionReminderHandler
{
    public static async Task HandleAsync(
        DeletionReminderDueEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            PrivacyDeletionReminderNotificationType.Instance,
            new PrivacyDeletionReminderNotificationData(evt.RequestId, evt.ScheduledDeletionAt),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
