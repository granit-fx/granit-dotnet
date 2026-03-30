using Granit.DataProtection;
using Granit.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when a user requests deferred deletion with a grace period (cooling-off).
/// Starts the <see cref="PersonalDataDeletionSaga"/> which schedules the actual deletion.
/// </summary>
public sealed record DeletionDeferredEto(
    Guid RequestId,
    Guid UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential)]
    string RequestedBy,
    DateTimeOffset RequestedAt,
    [property: SensitiveData(Level = Sensitivity.Confidential)]
    string Reason,
    DateTimeOffset ScheduledDeletionAt,
    string Regulation,
    string? TenantId = null) : IIntegrationEvent;
