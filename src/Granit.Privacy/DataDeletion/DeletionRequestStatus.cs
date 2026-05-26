using Granit.DataProtection;

namespace Granit.Privacy.DataDeletion;

/// <summary>
/// Read model for a deletion request. Returned by <see cref="IDeletionRequestTrackerReader"/>.
/// </summary>
public sealed record DeletionRequestStatus(
    Guid RequestId,
    Guid UserId,
    DeletionRequestState State,
    [property: SensitiveData(Level = Sensitivity.Confidential)]
    string Reason,
    DateTimeOffset RequestedAt,
    DateTimeOffset ScheduledDeletionAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ExecutedAt,
    string? Regulation = null,
    Guid? TenantId = null);
