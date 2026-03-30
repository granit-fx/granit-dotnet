using Granit.Events;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published when a data subject requests export of their personal data (GDPR Art. 15/20, LGPD Art. 18, CCPA).
/// Each registered data provider handles this event and prepares its fragment.
/// </summary>
public sealed record PersonalDataRequestedEto(
    Guid RequestId,
    Guid UserId,
    DateTimeOffset RequestedAt,
    string Regulation,
    string? TenantId = null,
    string RequestedFormat = "JSON") : IIntegrationEvent;
