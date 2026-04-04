using Granit.Persistence.EntityFrameworkCore;
using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfPlanWriter(
    IDbContextFactory<SubscriptionsDbContext> contextFactory)
    : EfStoreBase<Plan, SubscriptionsDbContext>(contextFactory),
      IPlanWriter
{
    Task IPlanWriter.AddAsync(Plan plan, CancellationToken cancellationToken) =>
        base.AddAsync(plan, cancellationToken);

    Task IPlanWriter.UpdateAsync(Plan plan, CancellationToken cancellationToken) =>
        base.UpdateAsync(plan, cancellationToken);
}
