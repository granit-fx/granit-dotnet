using Granit.DataExchange.Import.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="ImportJob"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all import jobs are returned cross-tenant.
/// </summary>
internal sealed class EfImportJobQueryableSource(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<ImportJob>, IAsyncDisposable, IDisposable
{
    private DataExchangeDbContext? _context;

    public IQueryable<ImportJob> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<ImportJob> query = _context.ImportJobs.AsNoTracking();
        return scope.Restrict(query, typeof(ImportJob).Name);
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
