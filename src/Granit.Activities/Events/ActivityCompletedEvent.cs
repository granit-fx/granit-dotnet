using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised when an <see cref="Domain.Activity"/> transitions to
/// <see cref="Domain.ActivityStatus.Done"/>. Local-bus dispatch.
/// </summary>
public sealed record ActivityCompletedEvent(
    Guid ActivityId,
    Guid CompletedByUserId,
    DateTimeOffset CompletedAt) : IDomainEvent;
