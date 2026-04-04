using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when a new plan price version is created.</summary>
public sealed record PlanPriceCreatedEto(
    Guid PlanId,
    Guid PlanPriceId,
    decimal Amount,
    string Currency,
    string Interval,
    DateTimeOffset EffectiveFrom) : IIntegrationEvent;
