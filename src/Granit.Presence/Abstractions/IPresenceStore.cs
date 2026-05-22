using Granit.Presence.Domain;

namespace Granit.Presence.Abstractions;

/// <summary>
/// Persistent storage for the manual presence override.
/// Default in-memory implementation is replaced by <c>Granit.Presence.EntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// The store owns the unit-of-work boundary for mutations. The EF Core implementation
/// loads the tracked aggregate, applies the mutator on the tracked instance, then calls
/// <c>SaveChangesAsync</c> so that <c>AuditedEntityInterceptor</c> populates
/// <c>CreatedBy</c>/<c>ModifiedAt</c> and the domain-event dispatcher picks up the
/// events accumulated by <see cref="UserPresence"/>.
/// </remarks>
public interface IPresenceStore
{
    /// <summary>Returns the override for the given user, or <c>null</c> when none exists.</summary>
    Task<UserPresence?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns the overrides for the given user IDs as a dictionary keyed by user ID.</summary>
    Task<IReadOnlyDictionary<Guid, UserPresence>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the user's presence (creating one via <paramref name="factory"/> when absent),
    /// applies <paramref name="mutator"/> on the tracked aggregate, and persists the result
    /// in a single unit of work. Returns the saved aggregate.
    /// </summary>
    Task<UserPresence> MutateAsync(
        Guid userId,
        Func<UserPresence> factory,
        Action<UserPresence> mutator,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the override for the given user. No-op when the row is absent.
    /// </summary>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
