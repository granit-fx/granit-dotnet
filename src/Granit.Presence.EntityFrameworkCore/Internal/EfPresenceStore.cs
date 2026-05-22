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
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(p => p.UserId);
    }

    public async Task<UserPresence> MutateAsync(
        Guid userId,
        Func<UserPresence> factory,
        Action<UserPresence> mutator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(mutator);

        await using PresenceDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        UserPresence? tracked = await context.UserPresences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (tracked is null)
        {
            tracked = factory();
            context.UserPresences.Add(tracked);
        }

        mutator(tracked);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tracked;
    }

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using PresenceDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        UserPresence? tracked = await context.UserPresences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken).ConfigureAwait(false);

        if (tracked is null)
        {
            return;
        }

        context.UserPresences.Remove(tracked);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
