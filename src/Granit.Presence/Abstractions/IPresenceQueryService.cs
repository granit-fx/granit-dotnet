using Granit.Presence.Domain;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Blends the manual override (<see cref="IPresenceStore"/>) and live heartbeat
/// (<see cref="IPresenceTracker"/>) into an effective <see cref="PresenceSnapshot"/>.
/// </summary>
public interface IPresenceQueryService
{
    /// <summary>Computes the effective snapshot for one user.</summary>
    Task<PresenceSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Computes effective snapshots for many users in a single batch operation
    /// (one store call + one tracker call). The result always contains an entry for
    /// every requested user — users with no override and no heartbeat yield an
    /// Offline snapshot.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PresenceSnapshot>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken);
}
