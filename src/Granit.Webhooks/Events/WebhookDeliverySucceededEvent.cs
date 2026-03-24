using Granit.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Raised when a webhook delivery succeeds.
/// Enables SLO tracking and delivery observability.
/// </summary>
/// <param name="SubscriptionId">The unique identifier of the subscription.</param>
/// <param name="DeliveredAt">Timestamp of the successful delivery.</param>
public sealed record WebhookDeliverySucceededEvent(
    Guid SubscriptionId,
    DateTimeOffset DeliveredAt) : IDomainEvent;
