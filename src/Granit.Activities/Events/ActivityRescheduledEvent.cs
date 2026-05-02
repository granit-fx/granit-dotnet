using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised when an <see cref="Domain.Activity"/>'s due date
/// changes. Local-bus dispatch.
/// </summary>
public sealed record ActivityRescheduledEvent(
    Guid ActivityId,
    DateTimeOffset PreviousDueAt,
    DateTimeOffset NewDueAt) : IDomainEvent;
