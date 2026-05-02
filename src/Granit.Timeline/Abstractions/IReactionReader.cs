using Granit.Timeline.Domain;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Read side of the reactions repository (CQRS split). Tenant filtering is
/// applied automatically by the persistence layer's standard query filter —
/// callers never constrain by <c>TenantId</c> directly.
/// </summary>
public interface IReactionReader
{
    /// <summary>Returns every reaction on a single timeline entry, ordered by <c>CreatedAt</c>.</summary>
    Task<IReadOnlyList<Reaction>> GetByEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup for the timeline stream endpoint — returns every reaction
    /// across the supplied entry ids in a single round trip. Empty input
    /// returns an empty list (no query issued).
    /// </summary>
    Task<IReadOnlyList<Reaction>> GetByEntriesAsync(
        IReadOnlyCollection<Guid> entryIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the (current user, entry, emoji) row when present, or
    /// <see langword="null"/>. Used by the toggle endpoint (story C2) to
    /// decide between Add and Remove without scanning the full entry.
    /// </summary>
    Task<Reaction?> FindAsync(
        Guid entryId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default);
}
