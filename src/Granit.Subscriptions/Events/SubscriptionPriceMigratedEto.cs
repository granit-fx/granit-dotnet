using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription is migrated to a new price version.</summary>
public sealed record SubscriptionPriceMigratedEto(
    Guid SubscriptionId,
    Guid TenantId,
    Guid? OldPlanPriceId,
    Guid NewPlanPriceId) : IIntegrationEvent;
