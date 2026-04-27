using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="Party"/>.</summary>
/// <remarks>
/// When no tenant context is active (host admin browsing host-scoped contacts), the
/// multi-tenant query filter is disabled so all contacts are returned cross-tenant.
/// Eager-loads addresses, emails, phones, and external mappings — admin grids show them.
/// </remarks>
internal sealed class EfPartyQueryableSource(
    IDbContextFactory<PartiesDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter) : IQueryableSource<Party>, IDisposable
{
    private readonly PartiesDbContext _context = contextFactory.CreateDbContext();
    private readonly IDisposable? _tenantBypass = !currentTenant.IsAvailable
        ? dataFilter.Disable<IMultiTenant>()
        : null;

    public IQueryable<Party> GetQueryable() =>
        _context.Parties
            .AsNoTracking()
            .Include(c => c.Addresses)
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .Include(c => c.ExternalMappings);

    public void Dispose()
    {
        _tenantBypass?.Dispose();
        _context.Dispose();
    }
}
