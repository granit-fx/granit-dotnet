using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

internal sealed class EfPlanReader(
    IDbContextFactory<SubscriptionsDbContext> contextFactory)
    : EfStoreBase<Plan, SubscriptionsDbContext>(contextFactory),
      IPlanReader
{
    public Task<Plan?> GetByIdAsync(PlanId id, CancellationToken cancellationToken = default) =>
        ReadAsync(
            async db => await Query(db)
                .Include(p => p.Prices)
                .FirstOrDefaultAsync(p => p.Id == id.Value, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<IReadOnlyList<Plan>> GetAvailablePlansAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(
            async db =>
            {
                List<Plan> plans = await Query(db)
                    .Include(p => p.Prices)
                    .Where(p => p.LifecycleStatus == WorkflowLifecycleStatus.Published)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return (IReadOnlyList<Plan>)plans;
            },
            cancellationToken);

    public Task<Plan?> GetByExternalIdAsync(
        string providerName, string externalId, CancellationToken cancellationToken = default) =>
        ReadAsync(async db => await db.Plans
            .Include(p => p.Prices)
            .Include(p => p.ExternalMappings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                p => p.ExternalMappings.Any(m => m.ProviderName == providerName && m.ExternalId == externalId),
                cancellationToken)
            .ConfigureAwait(false), cancellationToken);
}
