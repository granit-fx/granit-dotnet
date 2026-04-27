using Granit.Notifications.Abstractions;
using Granit.Webhooks.Events;

namespace Granit.Webhooks.Notifications.Handlers;

/// <summary>
/// Handles <see cref="WebhookDeliveryFailureThresholdExceededEto"/> by publishing a
/// <see cref="WebhooksDeliveryFailureThresholdNotificationType"/> to all opted-in
/// subscribers (typically tenant administrators).
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient strategy.</b> The Eto carries no user identifier — it is raised by the
/// <see cref="WebhookSubscription"/> aggregate against a subscription that may or may
/// not be tenant-scoped. To stay decoupled from any tenant-admin resolver, the bridge
/// publishes via <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>:
/// administrators opt in through the standard notifications subscription UI, and the
/// dispatch engine fans out to whoever is subscribed at delivery time.
/// </para>
/// <para>
/// <b>Wolverine routing — local.</b> The Eto is fired in-process by the webhooks
/// delivery handler (<c>WebhookSubscription.RecordFailure</c>) inside the same host
/// that loads <c>Granit.Webhooks.Notifications</c>. Wolverine subscribes this handler
/// via the local event bus — no broker hop, no outbox round-trip — preserving ordering
/// and minimising latency between threshold breach and admin alert.
/// </para>
/// </remarks>
public class DeliveryFailureThresholdNotificationHandler
{
    public static async Task HandleAsync(
        WebhookDeliveryFailureThresholdExceededEto evt,
        INotificationPublisher publisher,
        CancellationToken cancellationToken)
    {
        await publisher.PublishToSubscribersAsync(
            WebhooksDeliveryFailureThresholdNotificationType.Instance,
            new WebhooksDeliveryFailureThresholdNotificationData(
                evt.SubscriptionId,
                evt.TargetUrl,
                evt.ConsecutiveFailureCount),
            cancellationToken).ConfigureAwait(false);
    }
}
