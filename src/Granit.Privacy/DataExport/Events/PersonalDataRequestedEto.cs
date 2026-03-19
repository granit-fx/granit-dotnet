using Granit.Core.Events;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published when a data subject requests export of their personal data (RGPD Art. 15/20).
/// Each registered data provider handles this event and prepares its fragment.
/// </summary>
public sealed record PersonalDataRequestedEto(
    Guid RequestId,
    Guid UserId,
    DateTimeOffset RequestedAt) : IIntegrationEvent;
