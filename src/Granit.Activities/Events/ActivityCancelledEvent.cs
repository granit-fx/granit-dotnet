using Granit.Events;

namespace Granit.Activities.Events;

/// <summary>
/// Domain event raised when an <see cref="Domain.Activity"/> transitions to
/// <see cref="Domain.ActivityStatus.Cancelled"/>. Local-bus dispatch.
/// </summary>
public sealed record ActivityCancelledEvent(
    Guid ActivityId,
    Guid CancelledByUserId,
    DateTimeOffset CancelledAt) : IDomainEvent;
