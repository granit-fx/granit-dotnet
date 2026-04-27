using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription is created. Triggers billing setup.</summary>
public sealed record SubscriptionCreatedEto(
    Guid SubscriptionId,
    Guid PlanId,
    Guid TenantId,
    Guid PartyId,
    bool HasTrial) : IIntegrationEvent;
