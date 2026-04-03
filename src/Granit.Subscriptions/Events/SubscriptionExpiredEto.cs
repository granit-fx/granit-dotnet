using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a trial subscription expires without conversion.</summary>
public sealed record SubscriptionExpiredEto(
    Guid SubscriptionId,
    Guid TenantId) : IIntegrationEvent;
