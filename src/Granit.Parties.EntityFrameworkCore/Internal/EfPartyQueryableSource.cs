using Granit.Parties.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="Party"/>.</summary>
/// <remarks>
/// The standard <see cref="IMultiTenant"/> query filter is intentionally left active. When no
/// tenant context is in scope (host admin), the filter naturally restricts the result set to
/// host-scoped parties (<c>TenantId == null</c>) — exactly the documented invariant in
/// <see cref="EfPartyStore"/>. Disabling the filter in that branch would expose every tenant's
/// parties to a host-scoped browser, which is a privacy regression. Cross-tenant browsing
/// must be opt-in via <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c> from a dedicated,
/// permission-gated endpoint, not the default behaviour of this source.
/// </remarks>
internal sealed class EfPartyQueryableSource(
    IDbContextFactory<PartiesDbContext> contextFactory) : IQueryableSource<Party>, IDisposable
{
    private readonly PartiesDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<Party> GetQueryable() =>
        _context.Parties
            .AsNoTracking()
            .Include(c => c.Addresses)
            .Include(c => c.Emails)
            .Include(c => c.Phones)
            .Include(c => c.ExternalMappings);

    public void Dispose() => _context.Dispose();
}
