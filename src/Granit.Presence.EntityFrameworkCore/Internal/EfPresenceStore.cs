using Granit.Presence.Abstractions;
using Granit.Presence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Presence.EntityFrameworkCore.Internal;

internal sealed class EfPresenceStore(IDbContextFactory<PresenceDbContext> contextFactory) : IPresenceStore
{
    public async Task<UserPresence?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using PresenceDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.UserPresences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, UserPresence>> GetManyAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserPresence>(0);
        }

        await using PresenceDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<UserPresence> rows = await context.UserPresences
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(p => p.UserId);
    }

    public async Task UpsertAsync(UserPresence presence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presence);

        await using PresenceDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        UserPresence? existing = await context.UserPresences
            .FirstOrDefaultAsync(p => p.UserId == presence.UserId, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            await context.UserPresences.AddAsync(presence, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(presence);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using PresenceDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.UserPresences
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
