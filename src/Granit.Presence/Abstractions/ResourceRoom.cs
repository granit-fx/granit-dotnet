namespace Granit.Presence.Abstractions;

/// <summary>
/// Snapshot of the participants currently active inside a resource room.
/// </summary>
/// <param name="Resource">The room's resource identifier.</param>
/// <param name="Participants">
/// Live participants, de-duplicated by <see cref="ResourcePresenceEntry.UserId"/> and
/// filtered to those whose <see cref="ResourcePresenceEntry.LastSeenUtc"/> is within
/// <c>PresenceOptions.OfflineThreshold</c>.
/// </param>
public sealed record ResourceRoom(
    ResourceRef Resource,
    IReadOnlyList<ResourcePresenceEntry> Participants);
