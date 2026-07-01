using Granit.DataExchange.Export.Domain;
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
    ITenantQueryScope scope)
    : IQueryableSource<ExportJob>, IAsyncDisposable, IDisposable
{
    private DataExchangeDbContext? _context;

    public IQueryable<ExportJob> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<ExportJob> query = _context.ExportJobs.AsNoTracking();
        return scope.Restrict(query, typeof(ExportJob).Name);
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
