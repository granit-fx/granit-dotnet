using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription changes plan. Triggers Features cache invalidation.</summary>
public sealed record SubscriptionPlanChangedEto(
    Guid SubscriptionId,
    Guid OldPlanId,
    Guid NewPlanId,
    Guid TenantId) : IIntegrationEvent;
