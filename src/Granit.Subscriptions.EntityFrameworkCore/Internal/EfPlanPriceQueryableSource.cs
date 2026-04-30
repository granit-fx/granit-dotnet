using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="PlanPrice"/>. Required by the joined MRR / ARR metrics
/// (<c>JoinedMetricDefinition&lt;Subscription, PlanPrice, decimal&gt;</c>) so the
/// <c>MetricExecutor</c> can resolve the joined queryable at request time.
/// </summary>
/// <remarks>
/// Same lifecycle posture as <see cref="EfSubscriptionQueryableSource"/> — owns
/// a per-instance <see cref="DbContext"/> from the factory and disposes it.
/// When no tenant context is active (host admin), the multi-tenant query
/// filter is disabled so all plan prices are returned cross-tenant.
/// </remarks>
internal sealed class EfPlanPriceQueryableSource(
    IDbContextFactory<SubscriptionsDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter) : IQueryableSource<PlanPrice>, IDisposable
{
    private readonly SubscriptionsDbContext _context = contextFactory.CreateDbContext();
    private readonly IDisposable? _tenantBypass = !currentTenant.IsAvailable
        ? dataFilter.Disable<IMultiTenant>()
        : null;

    public IQueryable<PlanPrice> GetQueryable() =>
        _context.Set<PlanPrice>().AsNoTracking();

    public void Dispose()
    {
        _tenantBypass?.Dispose();
        _context.Dispose();
    }
}
