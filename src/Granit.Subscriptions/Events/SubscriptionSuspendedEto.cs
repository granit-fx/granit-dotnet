using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a subscription is suspended after exhausting payment retries.</summary>
public sealed record SubscriptionSuspendedEto(
    Guid SubscriptionId,
    Guid TenantId) : IIntegrationEvent;
