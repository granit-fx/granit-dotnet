namespace Granit.Presence.Abstractions;

/// <summary>
/// A single participant entry inside a <see cref="ResourceRoom"/>. Stored verbatim in the
/// presence cache and returned to clients via the resource-presence endpoints.
/// </summary>
/// <param name="UserId">The participant's user identifier.</param>
/// <param name="LastSeenUtc">
/// Server-side instant of the participant's most recent heartbeat into this room. Used to
/// expire stale entries on read (older than <c>PresenceOptions.OfflineThreshold</c>).
/// </param>
/// <param name="Metadata">
/// Optional opaque, caller-supplied UTF-8 string (≤ 512 bytes — enforced at write time)
/// — typically a JSON blob describing the participant's UI cursor / section.
/// </param>
public sealed record ResourcePresenceEntry(
    Guid UserId,
    DateTimeOffset LastSeenUtc,
    string? Metadata = null);
