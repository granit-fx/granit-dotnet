using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;

namespace Granit.Timeline.Internal;

/// <summary>
/// Default in-memory implementation of <see cref="IReactionReader"/> +
/// <see cref="IReactionWriter"/> — replaced by
/// <c>EfCoreReactionStore</c> when <c>Granit.Timeline.EntityFrameworkCore</c>
/// is loaded. Kept for development hosts and unit tests.
/// </summary>
internal sealed class InMemoryReactionStore : IReactionReader, IReactionWriter
{
    private readonly System.Threading.Lock _lock = new();
    private readonly List<Reaction> _store = [];

    public Task<IReadOnlyList<Reaction>> GetByEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<Reaction> rows = [.. _store
                .Where(r => r.EntryId == entryId)
                .OrderBy(r => r.CreatedAt)];
            return Task.FromResult(rows);
        }
    }

    public Task<IReadOnlyList<Reaction>> GetByEntriesAsync(
        IReadOnlyCollection<Guid> entryIds,
        CancellationToken cancellationToken = default)
    {
        if (entryIds is null || entryIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Reaction>>([]);
        }

        HashSet<Guid> set = [.. entryIds];
        lock (_lock)
        {
            IReadOnlyList<Reaction> rows = [.. _store
                .Where(r => set.Contains(r.EntryId))
                .OrderBy(r => r.CreatedAt)];
            return Task.FromResult(rows);
        }
    }

    public Task<Reaction?> FindAsync(
        Guid entryId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            Reaction? hit = _store.FirstOrDefault(r =>
                r.EntryId == entryId
                && r.UserId == userId
                && string.Equals(r.Emoji, emoji, StringComparison.Ordinal));
            return Task.FromResult(hit);
        }
    }

    public Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        lock (_lock)
        {
            if (_store.Any(r =>
                r.EntryId == reaction.EntryId
                && r.UserId == reaction.UserId
                && string.Equals(r.Emoji, reaction.Emoji, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Reaction ({reaction.EntryId}, {reaction.UserId}, {reaction.Emoji}) already exists.");
            }
            _store.Add(reaction);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        Guid entryId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _store.RemoveAll(r =>
                r.EntryId == entryId
                && r.UserId == userId
                && string.Equals(r.Emoji, emoji, StringComparison.Ordinal));
        }
        return Task.CompletedTask;
    }
}
