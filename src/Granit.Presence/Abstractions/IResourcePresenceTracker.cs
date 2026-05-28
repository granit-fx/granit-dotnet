namespace Granit.Presence.Abstractions;

/// <summary>
/// Resource-scoped multi-user awareness tracker — complements the user-keyed
/// <see cref="IPresenceTracker"/> with "who else is here?" semantics for collaborative
/// surfaces (CMS pages, dashboards, kanban cards…).
/// </summary>
/// <remarks>
/// <para>
/// Implementations MUST de-duplicate by <see cref="ResourcePresenceEntry.UserId"/> (multi-tab
/// safe), apply the MAX anti-flapping rule on <c>LastSeenUtc</c>, filter out entries older than
/// <c>PresenceOptions.OfflineThreshold</c> at read time, and remove the cache entry entirely
/// when the room becomes empty so no <c>{}</c> blob lingers.
/// </para>
/// <para>
/// Metadata is capped at 512 bytes serialized; oversize values are rejected with
/// <see cref="ArgumentException"/> at join time.
/// </para>
/// </remarks>
public interface IResourcePresenceTracker
{
    /// <summary>
    /// Joins (or heartbeats) the supplied user into the room and returns the resulting snapshot.
    /// Idempotent on <paramref name="userId"/>: a repeat call refreshes <c>LastSeenUtc</c>
    /// (MAX-merged) and overwrites the metadata.
    /// </summary>
    /// <param name="resource">The room's resource identifier.</param>
    /// <param name="userId">The joining user. MUST be a non-empty GUID.</param>
    /// <param name="metadata">
    /// Optional opaque caller-supplied UTF-8 string (≤ 512 bytes serialized). Pass <c>null</c>
    /// to clear any previously recorded metadata for the user in this room.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The room after the user has been merged in.</returns>
    Task<ResourceRoom> JoinAsync(
        ResourceRef resource,
        Guid userId,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the user from the room. No-op when the user is absent. When the resulting
    /// room is empty the cache entry is evicted entirely.
    /// </summary>
    Task LeaveAsync(
        ResourceRef resource,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the room snapshot — stale participants (older than
    /// <c>PresenceOptions.OfflineThreshold</c>) are filtered at read time.
    /// </summary>
    Task<ResourceRoom> GetAsync(
        ResourceRef resource,
        CancellationToken cancellationToken = default);
}
