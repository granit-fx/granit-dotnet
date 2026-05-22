namespace Granit.Presence.Domain;

/// <summary>
/// Computed presence snapshot returned by <see cref="Abstractions.IPresenceQueryService"/>.
/// </summary>
/// <param name="UserId">The user this snapshot describes.</param>
/// <param name="EffectiveStatus">The status to broadcast to other users.</param>
/// <param name="ManualOverride">
/// The current manual override, or <c>null</c> when the user has not set one
/// (i.e. <see cref="ManualPresenceStatus.Available"/>).
/// </param>
/// <param name="OverrideUntilUtc">Expiration of the manual override, or <c>null</c> for indefinite.</param>
/// <param name="LastSeenUtc">
/// Server-side instant of the most recent heartbeat. Equal to <see cref="DateTimeOffset.MinValue"/>
/// when the user has never been seen.
/// </param>
public sealed record PresenceSnapshot(
    Guid UserId,
    PresenceStatus EffectiveStatus,
    ManualPresenceStatus? ManualOverride,
    DateTimeOffset? OverrideUntilUtc,
    DateTimeOffset LastSeenUtc);
