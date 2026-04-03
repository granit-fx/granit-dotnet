using Granit.Guids;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfSeatWriter(
    IDbContextFactory<SubscriptionsDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IClock clock) : ISeatWriter
{
    public async Task AssignSeatAsync(
        SubscriptionId subscriptionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using SubscriptionsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        Subscription subscription = await context.Subscriptions
            .Include(s => s.Seats)
            .FirstAsync(s => s.Id == subscriptionId.Value, cancellationToken)
            .ConfigureAwait(false);

        var seat = SubscriptionSeat.Create(guidGenerator.Create(), userId, clock.Now);
        subscription.AssignSeat(seat);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RevokeSeatAsync(
        SubscriptionId subscriptionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using SubscriptionsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        Subscription subscription = await context.Subscriptions
            .Include(s => s.Seats)
            .FirstAsync(s => s.Id == subscriptionId.Value, cancellationToken)
            .ConfigureAwait(false);

        bool revoked = subscription.RevokeSeat(userId);
        if (revoked)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return revoked;
    }
}
