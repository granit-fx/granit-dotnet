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
        return await context.Seats
            .CountAsync(s => EF.Property<Guid>(s, "SubscriptionId") == subscriptionId.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SubscriptionSeat>> GetSeatsAsync(
        SubscriptionId subscriptionId,
        CancellationToken cancellationToken = default)
    {
        await using SubscriptionsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Seats
            .Where(s => EF.Property<Guid>(s, "SubscriptionId") == subscriptionId.Value)
            .OrderBy(s => s.AssignedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
