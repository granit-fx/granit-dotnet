using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfSeatReader(
    IDbContextFactory<SubscriptionsDbContext> contextFactory) : ISeatReader
{
    public async Task<int> GetSeatCountAsync(SubscriptionId subscriptionId, CancellationToken cancellationToken = default)
    {
        await using SubscriptionsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Subscriptions
            .Where(s => s.Id == subscriptionId.Value)
            .SelectMany(s => s.Seats)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SubscriptionSeat>> GetSeatsAsync(
        SubscriptionId subscriptionId,
        CancellationToken cancellationToken = default)
    {
        await using SubscriptionsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Subscriptions
            .Where(s => s.Id == subscriptionId.Value)
            .SelectMany(s => s.Seats)
            .OrderBy(s => s.AssignedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
