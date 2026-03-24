using Granit.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when a user requests deferred deletion with a grace period (cooling-off).
/// Starts the <see cref="GdprDeletionSaga"/> which schedules the actual deletion.
/// </summary>
public sealed record DeletionDeferredEto(
    Guid RequestId,
    Guid UserId,
    string RequestedBy,
    DateTimeOffset RequestedAt,
    string Reason,
    DateTimeOffset ScheduledDeletionAt) : IIntegrationEvent;
