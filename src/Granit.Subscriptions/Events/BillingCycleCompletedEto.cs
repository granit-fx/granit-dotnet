using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription billing period ends. Triggers invoice generation in Billing module.</summary>
public sealed record BillingCycleCompletedEto(
    Guid SubscriptionId,
    Guid PlanId,
    Guid TenantId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd) : IIntegrationEvent;
