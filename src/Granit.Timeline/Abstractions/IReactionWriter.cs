using Granit.Timeline.Domain;

namespace Granit.Timeline.Abstractions;

/// <summary>
/// Write side of the reactions repository. The unique
/// <c>(EntryId, UserId, Emoji)</c> index in the EF companion is the
/// authoritative idempotency guard — concurrent double-clicks collide on
/// <c>AddAsync</c> and resolve to a single row.
/// </summary>
public interface IReactionWriter
{
    /// <summary>Persists a new reaction row. Throws on unique-index violation.</summary>
    Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the matching reaction. Idempotent — removing a non-existent
    /// row is a no-op (no exception).
    /// </summary>
    Task RemoveAsync(
        Guid entryId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default);
}
