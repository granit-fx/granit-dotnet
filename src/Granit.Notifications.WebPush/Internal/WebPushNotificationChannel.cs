using System.Text.Json;
using Granit.Notifications.Abstractions;
using Lib.Net.Http.WebPush;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.WebPush.Internal;

/// <summary>
/// <see cref="INotificationChannel"/> implementation for W3C Web Push (VAPID).
/// Sends push notifications to all browser subscriptions of a user.
/// </summary>
/// <remarks>
/// Delivers to all subscriptions even if some fail. Failed subscriptions are accumulated
/// and re-thrown as an <see cref="AggregateException"/> so the caller (Wolverine handler)
/// can retry the entire delivery.
/// </remarks>
internal sealed partial class WebPushNotificationChannel(
    PushServiceClient pushServiceClient,
    IWebPushSubscriptionReader subscriptionReader,
    IWebPushSubscriptionWriter subscriptionWriter,
    ILogger<WebPushNotificationChannel> logger) : INotificationChannel
{
    /// <inheritdoc />
    public string Name => NotificationChannels.WebPush;

    /// <inheritdoc />
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebPushSubscriptionInfo> subscriptions = await subscriptionReader.GetSubscriptionsAsync(
            context.RecipientUserId, context.TenantId, cancellationToken).ConfigureAwait(false);

        if (subscriptions.Count == 0)
        {
            LogNoPushSubscriptions(context.RecipientUserId);
            return;
        }

        WebPushNotificationPayload payload = new()
        {
            NotificationId = context.NotificationId,
            NotificationTypeName = context.NotificationTypeName,
            Severity = context.Severity.ToString(),
            Data = context.Data,
            OccurredAt = context.OccurredAt,
        };

        string serializedPayload = JsonSerializer.Serialize(payload);
        List<Exception>? failures = null;

        foreach (WebPushSubscriptionInfo sub in subscriptions)
        {
            Lib.Net.Http.WebPush.PushSubscription pushSubscription = new()
            {
                Endpoint = sub.Endpoint,
            };
            pushSubscription.SetKey(PushEncryptionKeyName.P256DH, sub.P256dh);
            pushSubscription.SetKey(PushEncryptionKeyName.Auth, sub.Auth);

            PushMessage pushMessage = new(serializedPayload)
            {
                Urgency = PushMessageUrgency.Normal,
            };

            try
            {
                await pushServiceClient.RequestPushMessageDeliveryAsync(pushSubscription, pushMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (PushServiceClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                LogSubscriptionExpired(sub.Endpoint);
                await subscriptionWriter.RemoveSubscriptionAsync(context.RecipientUserId, sub.Endpoint, context.TenantId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPushDeliveryFailed(sub.Endpoint, ex);
                (failures ??= []).Add(ex);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException(
                $"Push delivery failed for {failures.Count}/{subscriptions.Count} subscription(s)",
                failures);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No push subscriptions for user {UserId}, skipping")]
    private partial void LogNoPushSubscriptions(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Push subscription expired for endpoint {Endpoint}, removing")]
    private partial void LogSubscriptionExpired(string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Push delivery failed for endpoint {Endpoint}")]
    private partial void LogPushDeliveryFailed(string endpoint, Exception exception);
}
