using Granit.Presence.Domain;
using Granit.Presence.Endpoints.Dtos;

namespace Granit.Presence.Endpoints.Internal;

internal static class PresenceResponseMapper
{
    public static PresenceResponse ToResponse(PresenceSnapshot snapshot) =>
        new(
            snapshot.UserId,
            snapshot.EffectiveStatus,
            snapshot.ManualOverride,
            snapshot.OverrideUntilUtc,
            snapshot.LastSeenUtc == DateTimeOffset.MinValue ? null : snapshot.LastSeenUtc);
}
