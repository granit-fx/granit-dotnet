using Granit.Notifications.Abstractions;
using Granit.Webhooks.Events;

namespace Granit.Webhooks.Notifications.Handlers;

/// <summary>
/// Handles <see cref="WebhookSigningKeyRotationDueEto"/> by publishing a
/// <see cref="WebhooksSigningKeyRotationDueNotificationType"/> to all opted-in
/// subscribers (typically tenant administrators).
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient strategy.</b> The Eto carries no user identifier — the rotation
/// scanner runs as a system job over every webhook subscription's signing keys.
/// To stay decoupled from any tenant-admin resolver, the bridge publishes via
/// <see cref="INotificationPublisher.PublishToSubscribersAsync{TData}"/>:
/// administrators opt in through the standard notifications subscription UI, and the
/// dispatch engine fans out to whoever is subscribed at delivery time.
/// </para>
/// <para>
/// <b>Wolverine routing — local.</b> The Eto is fired in-process by the rotation
/// scanner background job inside the same host that loads
/// <c>Granit.Webhooks.Notifications</c>. Wolverine subscribes this handler via the
/// local event bus — no broker hop, no outbox round-trip — preserving ordering and
/// minimising latency between scanner emission and admin alert.
/// </para>
/// <para>
/// <b>Days-until-expiry.</b> Computed once at handle time using <see cref="TimeProvider"/>
/// rather than embedding it in the Eto, so the value reflects the moment the alert
/// is dispatched (post-outbox) rather than the moment the scanner ran. Negative
/// values (key already expired between scan and dispatch) are clamped to zero so the
/// rendered email never reads "expires in -1 days".
/// </para>
/// </remarks>
public class SigningKeyRotationDueHandler
{
    public static async Task HandleAsync(
        WebhookSigningKeyRotationDueEto evt,
        INotificationPublisher publisher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int daysUntilExpiry = Math.Max(0, (int)Math.Ceiling((evt.ExpiresAt - now).TotalDays));

        await publisher.PublishToSubscribersAsync(
            WebhooksSigningKeyRotationDueNotificationType.Instance,
            new WebhooksSigningKeyRotationDueNotificationData(
                evt.SubscriptionId,
                evt.KeyId,
                evt.ExpiresAt,
                daysUntilExpiry),
            cancellationToken).ConfigureAwait(false);
    }
}
