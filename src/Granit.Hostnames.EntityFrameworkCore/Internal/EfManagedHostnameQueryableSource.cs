using Granit.Hostnames.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="ManagedHostname"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all managed hostnames are returned cross-tenant.
/// </summary>
internal sealed class EfManagedHostnameQueryableSource(
    IDbContextFactory<HostnamesDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<ManagedHostname>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private HostnamesDbContext? _context;

    public IQueryable<ManagedHostname> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<ManagedHostname> query = _context.ManagedHostnames.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        HostnamesDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
