using System.Diagnostics;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Diagnostics;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;

namespace Granit.Webhooks.Handlers;

/// <summary>
/// Wolverine handler that fans out a <see cref="WebhookTrigger"/> into one
/// <see cref="SendWebhookCommand"/> per active subscriber.
/// </summary>
/// <remarks>
/// <para>
/// The returned <see cref="IEnumerable{T}"/> is handled natively by Wolverine: each element
/// is published as an independent message within the same Outbox transaction as the trigger.
/// If the process crashes before the transaction commits, no commands are lost.
/// </para>
/// <para>
/// Tenant resolution priority: ambient <see cref="ICurrentTenant"/> when available,
/// falling back to <see cref="WebhookTrigger.TenantId"/> embedded in the message.
/// This supports both HTTP-context-driven and Outbox-driven execution.
/// </para>
/// </remarks>
public sealed class WebhookFanoutHandler(
    IWebhookSubscriptionReader subscriptionReader,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    WebhooksMetrics metrics)
{
    /// <summary>
    /// Resolves active subscribers and produces one <see cref="SendWebhookCommand"/> per subscriber.
    /// Returns an empty enumerable when no subscribers match — no exception is thrown.
    /// </summary>
    public async Task<IEnumerable<SendWebhookCommand>> HandleAsync(
        WebhookTrigger trigger,
        CancellationToken cancellationToken)
    {
        using Activity? activity = WebhooksActivitySource.Source.StartActivity(WebhooksActivitySource.Fanout);
        activity?.SetTag("webhooks.event_type", trigger.EventType);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : trigger.TenantId;

        IReadOnlyList<Domain.WebhookSubscription> subscriptions =
            await subscriptionReader.GetActiveSubscriptionsAsync(
                trigger.EventType,
                tenantId,
                cancellationToken).ConfigureAwait(false);

        if (subscriptions.Count == 0)
        {
            activity?.SetTag("webhooks.subscriber_count", 0);
            return [];
        }

        WebhookEnvelope envelope = new()
        {
            EventId = trigger.EventId,
            EventType = trigger.EventType,
            TenantId = tenantId,
            Timestamp = trigger.OccurredAt,
            ApiVersion = WebhooksConstants.ApiVersion,
            Data = trigger.Payload,
        };

        activity?.SetTag("webhooks.subscriber_count", subscriptions.Count);
        metrics.RecordFanoutTriggered(tenantId?.ToString(), trigger.EventType);

        return subscriptions.Select(sub => new SendWebhookCommand
        {
            DeliveryId = guidGenerator.Create(),
            SubscriptionId = sub.Id,
            TargetUrl = sub.TargetUrl,
            SigningSecret = sub.SigningSecret,
            Envelope = envelope,
        });
    }
}
