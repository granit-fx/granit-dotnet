using Granit.Metering.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="MeterDefinition"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is bypassed
/// so all meter definitions are returned cross-tenant — required for platform-wide administration.
/// </summary>
internal sealed class EfMeterDefinitionQueryableSource(
    IDbContextFactory<MeteringDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<MeterDefinition>
{
    private readonly MeteringDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<MeterDefinition> GetQueryable()
    {
        IQueryable<MeterDefinition> query = _context.MeterDefinitions.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
