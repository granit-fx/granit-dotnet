using Granit.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Raised when a new webhook subscription is registered.
/// Enables subscriber audit trail.
/// </summary>
/// <param name="SubscriptionId">The unique identifier of the subscription.</param>
/// <param name="EventType">Logical event type the subscription is registered for.</param>
/// <param name="TargetUrl">The HTTPS endpoint receiving webhook deliveries.</param>
public sealed record WebhookSubscriptionCreatedEvent(
    Guid SubscriptionId,
    string EventType,
    string TargetUrl) : IDomainEvent;
