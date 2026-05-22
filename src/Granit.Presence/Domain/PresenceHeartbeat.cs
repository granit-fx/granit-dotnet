namespace Granit.Presence.Domain;

/// <summary>
/// Live heartbeat data for a user, stored in the presence cache (not in EF Core).
/// </summary>
/// <param name="LastPollUtc">Server-side instant of the most recent heartbeat received.</param>
/// <param name="LastActivityUtc">
/// Reconstructed instant of the user's most recent activity, derived from
/// <c>LastPollUtc - idleDuration</c>. Multi-tab clients are merged using
/// <c>MAX(existing.LastActivityUtc, computed.LastActivityUtc)</c> to prevent flapping.
/// </param>
public sealed record PresenceHeartbeat(
    DateTimeOffset LastPollUtc,
    DateTimeOffset LastActivityUtc);
