namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Response returned when a deferred deletion request is accepted.
/// </summary>
/// <param name="RequestId">Unique identifier for the deletion request (use to cancel or check status).</param>
/// <param name="ScheduledDeletionAt">Date and time when data will be permanently deleted if not cancelled.</param>
public sealed record PrivacyDeletionRequestResponse(
    Guid RequestId,
    DateTimeOffset ScheduledDeletionAt);
