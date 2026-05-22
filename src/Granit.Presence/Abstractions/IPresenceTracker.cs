using Granit.Presence.Domain;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Live heartbeat tracker. Records the most recent poll for a user and exposes the
/// resulting <see cref="PresenceHeartbeat"/> to the query service.
/// </summary>
/// <remarks>
/// Implementations MUST apply the multi-tab anti-flapping rule:
/// <c>LastActivityUtc = MAX(existing.LastActivityUtc, computed.LastActivityUtc)</c>.
/// Implementations SHOULD use a cache (e.g. FusionCache) with a TTL slightly larger
/// than <c>PresenceOptions.OfflineThreshold</c> so an absent entry naturally means
/// the user is offline.
/// </remarks>
public interface IPresenceTracker
{
    /// <summary>
    /// Records a heartbeat for the given user with the supplied client-side idle duration.
    /// </summary>
    /// <param name="userId">The user reporting activity.</param>
    /// <param name="idleDuration">
    /// Client-reported idle duration (seconds since last keyboard / mouse / scroll). MUST be
    /// non-negative; implementations clamp at <c>2 × OfflineThreshold</c> to defend against
    /// malicious or buggy clients.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The merged heartbeat after applying the MAX rule.</returns>
    Task<PresenceHeartbeat> RecordPollAsync(
        Guid userId,
        TimeSpan idleDuration,
        CancellationToken cancellationToken);

    /// <summary>Returns the current heartbeat for the user, or <c>null</c> when absent / expired.</summary>
    Task<PresenceHeartbeat?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the current heartbeats for the given user IDs. Missing entries are omitted
    /// from the result (not present as <c>null</c> entries).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PresenceHeartbeat>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Removes the cached heartbeat for the user. No-op when absent.</summary>
    Task RemoveAsync(Guid userId, CancellationToken cancellationToken);
}
