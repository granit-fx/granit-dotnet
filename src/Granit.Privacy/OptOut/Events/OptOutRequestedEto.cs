using Granit.DataProtection;
using Granit.Events;

namespace Granit.Privacy.OptOut.Events;

/// <summary>
/// Published when a user or anonymous visitor opts out of data sale/sharing (CCPA).
/// </summary>
public sealed record OptOutRequestedEto(
    Guid Id,
    Guid? UserId,
    [property: SensitiveData(Level = Sensitivity.Internal)]
    string? AnonymousTrackId,
    DateTimeOffset RequestedAt,
    string Regulation,
    Guid? TenantId) : IIntegrationEvent;
