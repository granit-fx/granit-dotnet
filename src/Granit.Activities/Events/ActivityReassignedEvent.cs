using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised when an <see cref="Domain.Activity"/>'s assignee
/// changes. Local-bus dispatch — used by notifications (story A7) to alert
/// the new assignee.
/// </summary>
public sealed record ActivityReassignedEvent(
    Guid ActivityId,
    Guid PreviousAssigneeUserId,
    Guid NewAssigneeUserId) : IDomainEvent;
