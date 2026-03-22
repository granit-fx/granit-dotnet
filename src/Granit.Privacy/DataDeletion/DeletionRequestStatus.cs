namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Read model for a deferred deletion request. Returned by <see cref="IDeletionRequestTrackerReader"/>.
/// </summary>
public sealed record DeletionRequestStatus(
    Guid RequestId,
    Guid UserId,
    DeletionRequestState State,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset ScheduledDeletionAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ExecutedAt);
