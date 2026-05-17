using Granit.BlobStorage.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="BlobDescriptor"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all blob descriptors are returned cross-tenant.
/// </summary>
internal sealed class EfBlobQueryableSource(
    IDbContextFactory<BlobStorageDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<BlobDescriptor>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private BlobStorageDbContext? _context;

    public IQueryable<BlobDescriptor> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<BlobDescriptor> query = _context.Blobs.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        BlobStorageDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
