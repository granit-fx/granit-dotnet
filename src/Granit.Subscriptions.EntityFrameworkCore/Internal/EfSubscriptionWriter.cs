using Granit.Persistence.EntityFrameworkCore;
using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfSubscriptionWriter(
    IDbContextFactory<SubscriptionsDbContext> contextFactory)
    : EfStoreBase<Subscription, SubscriptionsDbContext>(contextFactory),
      ISubscriptionWriter
{
    Task ISubscriptionWriter.AddAsync(Subscription subscription, CancellationToken cancellationToken) =>
        base.AddAsync(subscription, cancellationToken);

    Task ISubscriptionWriter.UpdateAsync(Subscription subscription, CancellationToken cancellationToken) =>
        base.UpdateAsync(subscription, cancellationToken);
}
