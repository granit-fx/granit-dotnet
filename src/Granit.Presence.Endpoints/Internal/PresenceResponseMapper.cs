using Granit.Presence.Domain;
using Granit.Presence.Endpoints.Dtos;

namespace Granit.Presence.Endpoints.Internal;

internal static class PresenceResponseMapper
{
    /// <summary>
    /// Maps a <see cref="PresenceSnapshot"/> to its API response.
    /// When the caller is not the snapshot's owner and the user has chosen
    /// <see cref="ManualPresenceStatus.AppearOffline"/>, the response is canonicalized
    /// to look exactly like a genuinely-offline user (no <c>LastSeenUtc</c>,
    /// no <c>ManualOverride</c>, no <c>OverrideUntilUtc</c>). This preserves the
    /// privacy contract of the <c>AppearOffline</c> status.
    /// </summary>
    /// <summary>
    /// Canonical "no information" response. Used for unknown users, never-seen users,
    /// and policy-denied targets — keeping the response shape indistinguishable so the
    /// endpoint cannot be used as a user-existence oracle.
    /// </summary>
    public static PresenceResponse Unknown(Guid userId) =>
        new(userId, PresenceStatus.Offline, ManualOverride: null, OverrideUntilUtc: null, LastSeenUtc: null);

    public static PresenceResponse ToResponse(PresenceSnapshot snapshot, bool isSelf)
    {
        if (!isSelf && snapshot.ManualOverride == ManualPresenceStatus.AppearOffline)
        {
            return new PresenceResponse(
                snapshot.UserId,
                PresenceStatus.Offline,
                ManualOverride: null,
                OverrideUntilUtc: null,
                LastSeenUtc: null);
        }

        return new PresenceResponse(
            snapshot.UserId,
            snapshot.EffectiveStatus,
            snapshot.ManualOverride,
            snapshot.OverrideUntilUtc,
            snapshot.LastSeenUtc == DateTimeOffset.MinValue ? null : snapshot.LastSeenUtc);
    }
}
