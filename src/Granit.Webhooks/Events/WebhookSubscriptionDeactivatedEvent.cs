using Granit.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Raised when a webhook subscription is permanently deactivated.
/// </summary>
public sealed record WebhookSubscriptionDeactivatedEvent(
    Guid SubscriptionId,
    string Reason) : IDomainEvent;
