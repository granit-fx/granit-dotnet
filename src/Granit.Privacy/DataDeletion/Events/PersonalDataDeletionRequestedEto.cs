using Granit.DataProtection;
using Granit.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when a data subject requests deletion of their personal data (GDPR Art. 17, LGPD Art. 18, CCPA).
/// Each registered data provider handles this event and decides what can be deleted.
/// </summary>
public sealed record PersonalDataDeletionRequestedEto(
    Guid RequestId,
    Guid UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential)]
    string RequestedBy,
    DateTimeOffset RequestedAt,
    [property: SensitiveData(Level = Sensitivity.Confidential)]
    string Reason,
    string Regulation,
    string? TenantId = null) : IIntegrationEvent;
