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
    : IQueryableSource<Tenant>, IAsyncDisposable, IDisposable
{
    private MultiTenancyDbContext? _context;

    public IQueryable<Tenant> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        return _context.Tenants.AsNoTracking();
    }

    public ValueTask DisposeAsync()
    {
        MultiTenancyDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
