using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.Subscriptions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="Subscription"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// disabled so all subscriptions are returned cross-tenant.
/// </summary>
internal sealed class EfSubscriptionQueryableSource(
    IDbContextFactory<SubscriptionsDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter) : IQueryableSource<Subscription>, IDisposable
{
    private readonly SubscriptionsDbContext _context = contextFactory.CreateDbContext();
    private readonly IDisposable? _tenantBypass = !currentTenant.IsAvailable
        ? dataFilter.Disable<IMultiTenant>()
        : null;

    public IQueryable<Subscription> GetQueryable() =>
        _context.Subscriptions.AsNoTracking();

    public void Dispose()
    {
        _tenantBypass?.Dispose();
        _context.Dispose();
    }
}
