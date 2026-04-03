using Granit.Events;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions.Events;

/// <summary>Raised when a new subscription is created.</summary>
public sealed record SubscriptionCreatedEvent(
    Guid SubscriptionId,
    PlanId PlanId,
    Guid TenantId) : IDomainEvent;
