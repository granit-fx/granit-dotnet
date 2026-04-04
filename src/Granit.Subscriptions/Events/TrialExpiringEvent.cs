using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Raised by BackgroundJob when a trial expires within 3 days.</summary>
public sealed record TrialExpiringEvent(
    Guid SubscriptionId,
    Guid PlanId,
    int DaysRemaining) : IDomainEvent;
