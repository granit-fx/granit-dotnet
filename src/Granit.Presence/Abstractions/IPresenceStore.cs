using Granit.Presence.Domain;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Persistent storage for the manual presence override.
/// Default in-memory implementation is replaced by <c>Granit.Presence.EntityFrameworkCore</c>.
/// </summary>
public interface IPresenceStore
{
    /// <summary>Returns the override for the given user, or <c>null</c> when none exists.</summary>
    Task<UserPresence?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the overrides for the given user IDs as a dictionary keyed by user ID.</summary>
    Task<IReadOnlyDictionary<Guid, UserPresence>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>Inserts or updates the override for the given aggregate.</summary>
    Task UpsertAsync(UserPresence presence, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the override for the given user. No-op when the row is absent.
    /// </summary>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
