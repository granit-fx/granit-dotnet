using Granit.MultiTenancy.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="Tenant"/>.
/// Provides a read-only queryable over the tenant table for use by the query engine.
/// </summary>
internal sealed class EfTenantQueryableSource(
    IDbContextFactory<MultiTenancyDbContext> contextFactory)
    : IQueryableSource<Tenant>, IDisposable
{
    private readonly MultiTenancyDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<Tenant> GetQueryable() =>
        _context.Tenants.AsNoTracking();

    public void Dispose() => _context.Dispose();
}
