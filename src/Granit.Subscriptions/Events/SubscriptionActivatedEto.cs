using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription transitions to Active (trial conversion or first payment).</summary>
public sealed record SubscriptionActivatedEto(
    Guid SubscriptionId,
    Guid PlanId,
    Guid TenantId,
    Guid PartyId) : IIntegrationEvent;
