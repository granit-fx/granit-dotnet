using Granit.DataProtection;
using Granit.Events;

namespace Granit.Privacy.OptOut.Events;

/// <summary>
/// Published when a user revokes their opt-out of data sale/sharing.
/// </summary>
public sealed record OptOutRevokedEto(
    Guid Id,
    Guid? UserId,
    [property: SensitiveData(Level = Sensitivity.Internal)]
    string? AnonymousTrackId,
    DateTimeOffset RevokedAt,
    string Regulation,
    string? TenantId) : IIntegrationEvent;
