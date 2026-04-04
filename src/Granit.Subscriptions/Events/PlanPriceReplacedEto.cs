using Granit.Events;

namespace Granit.Subscriptions.Events;

/// <summary>Published when an existing plan price is replaced by a newer version.</summary>
public sealed record PlanPriceReplacedEto(
    Guid PlanId,
    Guid OldPriceId,
    Guid NewPriceId,
    decimal OldAmount,
    decimal NewAmount,
    string Currency,
    string Interval) : IIntegrationEvent;
