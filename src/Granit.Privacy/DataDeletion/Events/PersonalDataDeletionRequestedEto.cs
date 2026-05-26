using Granit.DataProtection;
using Granit.Encryption;
using Granit.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when a data subject requests deletion of their personal data (GDPR Art. 17, LGPD Art. 18, CCPA).
/// Each registered data provider handles this event and decides what can be deleted.
/// </summary>
public sealed record PersonalDataDeletionRequestedEto(
    Guid RequestId,
    Guid UserId,
    [property: SensitiveData(Level = Sensitivity.Confidential), Encrypted]
    string RequestedBy,
    DateTimeOffset RequestedAt,
    [property: SensitiveData(Level = Sensitivity.Confidential), Encrypted]
    string Reason,
    string Regulation,
    Guid? TenantId = null) : IIntegrationEvent;
