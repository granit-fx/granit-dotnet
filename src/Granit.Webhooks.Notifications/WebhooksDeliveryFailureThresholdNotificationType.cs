using Granit.Notifications;

namespace Granit.Webhooks.Notifications;

/// <summary>
/// Notification type raised when a webhook subscription crosses its consecutive
/// delivery-failure threshold (5 by default). Lets administrators investigate the
/// failing endpoint before events are silently lost or the subscription is
/// auto-suspended.
/// </summary>
/// <remarks>
/// Channels: Email + InApp. <see cref="NotificationSeverity.Warning"/> because the
/// subscription is still active at this point — the threshold is an early-warning
/// signal, not a hard failure. Recipients are resolved via the notification
/// subscription system (admins opt in through the admin UI), which keeps this
/// bridge free of any tenant-admin lookup service.
/// </remarks>
public sealed class WebhooksDeliveryFailureThresholdNotificationType
    : NotificationType<WebhooksDeliveryFailureThresholdNotificationData>
{
    /// <summary>Singleton instance.</summary>
    public static readonly WebhooksDeliveryFailureThresholdNotificationType Instance = new();

    /// <inheritdoc />
    public override string Name => "webhooks.delivery_failure_threshold";

    /// <inheritdoc />
    public override NotificationSeverity DefaultSeverity => NotificationSeverity.Warning;

    /// <inheritdoc />
    public override IReadOnlyList<string> DefaultChannels { get; } =
        [NotificationChannels.Email, NotificationChannels.InApp];
}

/// <summary>
/// Data payload for the webhook delivery-failure threshold notification.
/// </summary>
/// <param name="SubscriptionId">Identifier of the failing webhook subscription.</param>
/// <param name="TargetUrl">The HTTPS endpoint that is failing. Sourced from the
/// subscription's validated <c>HttpsUrl</c> (no query-string secrets accepted at
/// creation), so it is safe to expose in the rendered email.</param>
/// <param name="ConsecutiveFailureCount">Number of consecutive delivery failures
/// observed when the threshold was crossed.</param>
public sealed record WebhooksDeliveryFailureThresholdNotificationData(
    Guid SubscriptionId,
    string TargetUrl,
    int ConsecutiveFailureCount);
