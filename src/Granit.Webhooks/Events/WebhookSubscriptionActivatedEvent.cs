using Granit.Events;

namespace Granit.Webhooks.Events;

/// <summary>
/// Raised when a suspended webhook subscription is activated.
/// </summary>
public sealed record WebhookSubscriptionActivatedEvent(
    Guid SubscriptionId) : IDomainEvent;
