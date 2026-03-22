namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Status of a deferred deletion request.
/// </summary>
/// <param name="RequestId">Unique identifier for the deletion request.</param>
/// <param name="State">Current state: Deferred, Executed, or Cancelled.</param>
/// <param name="Reason">User-provided reason for the deletion request.</param>
/// <param name="RequestedAt">When the deletion was originally requested.</param>
/// <param name="ScheduledDeletionAt">When the data will be permanently deleted.</param>
/// <param name="CancelledAt">When the request was cancelled (null if not cancelled).</param>
/// <param name="ExecutedAt">When the deletion was executed (null if not yet executed).</param>
public sealed record PrivacyDeletionStatusResponse(
    Guid RequestId,
    string State,
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset ScheduledDeletionAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ExecutedAt);
