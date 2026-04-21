using Granit.Metering.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="UsageAggregate"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is bypassed
/// so usage rollups are returned cross-tenant — required for platform-wide usage review.
/// </summary>
internal sealed class EfUsageAggregateQueryableSource(
    IDbContextFactory<MeteringDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<UsageAggregate>
{
    private readonly MeteringDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<UsageAggregate> GetQueryable()
    {
        IQueryable<UsageAggregate> query = _context.UsageAggregates.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
