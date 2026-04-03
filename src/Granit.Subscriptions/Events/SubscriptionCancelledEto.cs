using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription is cancelled. Triggers final invoice and provider sync.</summary>
public sealed record SubscriptionCancelledEto(
    Guid SubscriptionId,
    Guid PlanId,
    Guid TenantId,
    string? Reason) : IIntegrationEvent;
