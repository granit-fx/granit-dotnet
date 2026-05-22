using Granit.Presence.Domain;

namespace Granit.Presence.Endpoints.Dtos;

/// <summary>
/// Presence snapshot returned by the API.
/// </summary>
/// <param name="UserId">The user this snapshot describes.</param>
/// <param name="EffectiveStatus">Status to broadcast to other users.</param>
/// <param name="ManualOverride">
/// Active manual override, or <c>null</c> when the user has not set one
/// (i.e. <see cref="ManualPresenceStatus.Available"/>).
/// </param>
/// <param name="OverrideUntilUtc">Expiration of the manual override, or <c>null</c> for indefinite.</param>
/// <param name="LastSeenUtc">Server-side instant of the most recent heartbeat, or <c>null</c> when never seen.</param>
public sealed record PresenceResponse(
    Guid UserId,
    PresenceStatus EffectiveStatus,
    ManualPresenceStatus? ManualOverride,
    DateTimeOffset? OverrideUntilUtc,
    DateTimeOffset? LastSeenUtc);
