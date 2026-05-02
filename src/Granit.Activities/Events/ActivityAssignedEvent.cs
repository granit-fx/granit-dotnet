using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised when an <see cref="Domain.Activity"/> is created and
/// assigned to a user. Local-bus dispatch — used by
/// <c>Granit.Activities.Notifications</c> (story A7) to send the assignment
/// notification.
/// </summary>
/// <param name="ActivityId">The newly created activity's id.</param>
/// <param name="Type">The activity type name (e.g. <c>"Call"</c>).</param>
/// <param name="AssignedToUserId">The user who should perform the activity.</param>
/// <param name="DueAt">When the activity should be performed by.</param>
/// <param name="EntityType">Polymorphic FK — host entity wire identifier.</param>
/// <param name="EntityId">Polymorphic FK — host entity row id.</param>
public sealed record ActivityAssignedEvent(
    Guid ActivityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    string EntityType,
    Guid EntityId) : IDomainEvent;
