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
    : IQueryableSource<BlobDescriptor>
{
    private readonly BlobStorageDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<BlobDescriptor> GetQueryable()
    {
        IQueryable<BlobDescriptor> query = _context.Blobs.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}
