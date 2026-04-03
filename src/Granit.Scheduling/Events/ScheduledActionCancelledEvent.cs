using Granit.Events;

namespace Granit.Scheduling.Events;

/// <summary>
/// Raised when a scheduled action is cancelled before execution.
/// </summary>
/// <param name="ActionId">The scheduled action identifier.</param>
/// <param name="CorrelationId">The optional correlation identifier linking to a domain entity.</param>
/// <param name="CancelledBy">The user who cancelled the action.</param>
public sealed record ScheduledActionCancelledEvent(
    Guid ActionId,
    string? CorrelationId,
    string? CancelledBy) : IDomainEvent;
