using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IReactionReader"/> +
/// <see cref="IReactionWriter"/>. Tenant filtering is applied automatically
/// by the DbContext's standard query filter.
/// </summary>
internal sealed class EfCoreReactionStore(IDbContextFactory<TimelineDbContext> contextFactory)
    : IReactionReader, IReactionWriter
{
    public async Task<IReadOnlyList<Reaction>> GetByEntryAsync(
        Guid entryId, CancellationToken cancellationToken = default)
    {
        await using TimelineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Reactions
            .AsNoTracking()
            .Where(r => r.EntryId == entryId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Reaction>> GetByEntriesAsync(
        IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken = default)
    {
        if (entryIds is null || entryIds.Count == 0)
        {
            return [];
        }

        await using TimelineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Reactions
            .AsNoTracking()
            .Where(r => entryIds.Contains(r.EntryId))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Reaction?> FindAsync(
        Guid entryId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        await using TimelineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Reactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.EntryId == entryId && r.UserId == userId && r.Emoji == emoji,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        await using TimelineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Reactions.Add(reaction);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        Guid entryId, Guid userId, string emoji, CancellationToken cancellationToken = default)
    {
        await using TimelineDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Reactions
            .Where(r => r.EntryId == entryId && r.UserId == userId && r.Emoji == emoji)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
