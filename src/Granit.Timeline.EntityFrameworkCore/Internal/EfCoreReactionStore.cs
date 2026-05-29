using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IReactionReader"/> + <see cref="IReactionWriter"/>.
/// Dispatches through <see cref="TimelineContextResolver"/> — reads on a known entry id
/// fan out across candidate contexts (entries can live in host or tenant DB under
/// Segregated) and return the first hit; writes route on <c>reaction.TenantId</c>.
/// </summary>
internal sealed class EfCoreReactionStore(TimelineContextResolver resolver)
    : IReactionReader, IReactionWriter
{
    public async Task<IReadOnlyList<Reaction>> GetByEntryAsync(
        Guid entryId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ITimelineDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Reaction> results = [];
            foreach (ITimelineDbContext db in contexts)
            {
                List<Reaction> partial = await db.Reactions
                    .AsNoTracking()
                    .Where(r => r.EntryId == entryId)
                    .OrderBy(r => r.CreatedAt)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(partial);
            }
            return results;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<Reaction>> GetByEntriesAsync(
        IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken = default)
    {
        if (entryIds is null || entryIds.Count == 0)
        {
            return [];
        }

        IReadOnlyList<ITimelineDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Reaction> results = [];
            foreach (ITimelineDbContext db in contexts)
            {
                List<Reaction> partial = await db.Reactions
                    .AsNoTracking()
                    .Where(r => entryIds.Contains(r.EntryId))
                    .OrderBy(r => r.CreatedAt)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(partial);
            }
            return results;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    public async Task<Reaction?> FindAsync(
        Guid entryId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ITimelineDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ITimelineDbContext db in contexts)
            {
                Reaction? hit = await db.Reactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.EntryId == entryId && r.UserId == userId && r.Emoji == emoji,
                        cancellationToken).ConfigureAwait(false);
                if (hit is not null)
                {
                    return hit;
                }
            }
            return null;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    public async Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        await using ITimelineDbContext db = await resolver
            .OpenForScopeAsync(reaction.TenantId, cancellationToken).ConfigureAwait(false);
        db.Reactions.Add(reaction);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        Guid entryId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ITimelineDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ITimelineDbContext db in contexts)
            {
                await db.Reactions
                    .Where(r => r.EntryId == entryId && r.UserId == userId && r.Emoji == emoji)
                    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    private static async Task DisposeAllAsync(IReadOnlyList<ITimelineDbContext> contexts)
    {
        foreach (ITimelineDbContext db in contexts)
        {
            await db.DisposeAsync().ConfigureAwait(false);
        }
    }
}
