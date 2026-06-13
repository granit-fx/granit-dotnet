using Granit.DataExchange.Export.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="ExportJob"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all export jobs are returned cross-tenant.
/// </summary>
internal sealed class EfExportJobQueryableSource(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<ExportJob>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private DataExchangeDbContext? _context;

    public IQueryable<ExportJob> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<ExportJob> query = _context.ExportJobs.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        DataExchangeDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
