using Granit.Persistence.EntityFrameworkCore;
using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfPlanWriter(
    IDbContextFactory<SubscriptionsDbContext> contextFactory)
    : EfStoreBase<Plan, SubscriptionsDbContext>(contextFactory),
      IPlanWriter
{
    Task IPlanWriter.AddAsync(Plan plan, CancellationToken cancellationToken) =>
        base.AddAsync(plan, cancellationToken);

    Task IPlanWriter.UpdateAsync(Plan plan, CancellationToken cancellationToken) =>
        base.WriteAsync(async db =>
        {
            // DbSet.Update() marks the entire disconnected graph as Modified.
            // PlanPrice entities added in memory (e.g., via Plan.AddPriceVersion)
            // don't exist in the database yet — mark them as Added to avoid
            // DbUpdateConcurrencyException (UPDATE targeting a non-existent row).
            var existingPriceIds = (await db.Set<PlanPrice>()
                .AsNoTracking()
                .Where(p => EF.Property<Guid>(p, "PlanId") == plan.Id)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToHashSet();

            db.Set<Plan>().Update(plan);

            foreach (EntityEntry<PlanPrice> entry in db.ChangeTracker.Entries<PlanPrice>())
            {
                if (entry.State == EntityState.Modified
                    && !existingPriceIds.Contains(entry.Entity.Id))
                {
                    entry.State = EntityState.Added;
                }
            }
        }, cancellationToken);
}
