using Granit.DataProtection;
using Granit.Encryption;
using Granit.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when a user requests deferred deletion with a grace period (cooling-off).
/// Starts the <c>PersonalDataDeletionSaga</c> (in <c>Granit.Privacy.Wolverine</c>) which schedules the actual deletion.
/// </summary>
public sealed record DeletionDeferredEto(
    [property: SagaIdentity] Guid RequestId,
    Guid UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential), Encrypted]
    string RequestedBy,
    DateTimeOffset RequestedAt,
    [property: SensitiveData(Level = Sensitivity.Confidential), Encrypted]
    string Reason,
    DateTimeOffset ScheduledDeletionAt,
    string Regulation,
    Guid? TenantId = null) : IIntegrationEvent;
