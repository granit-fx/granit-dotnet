using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Privacy.Notifications.Handlers;

/// <summary>
/// Handles <see cref="PersonalDataDeletionRequestedEto"/> by sending an immediate
/// acknowledgement to the data subject. GDPR Art. 12 §3 obliges the controller to
/// inform the subject of the action taken on their request "without undue delay" —
/// this email is the timestamped paper trail for that obligation.
/// </summary>
public class DeletionAcknowledgedHandler
{
    public static async Task HandleAsync(
        PersonalDataDeletionRequestedEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            new PrivacyDeletionAcknowledgedNotificationData(
                evt.RequestId,
                evt.RequestedAt,
                evt.Regulation),
            [evt.UserId.ToString()],
            cancellationToken).ConfigureAwait(false);
    }
}
