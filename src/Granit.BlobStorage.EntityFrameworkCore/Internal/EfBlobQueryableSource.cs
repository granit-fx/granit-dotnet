using Granit.BlobStorage.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="BlobDescriptor"/>.
/// </summary>
internal sealed class EfBlobQueryableSource(IDbContextFactory<BlobStorageDbContext> contextFactory)
    : IQueryableSource<BlobDescriptor>
{
    private readonly BlobStorageDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<BlobDescriptor> GetQueryable() =>
        _context.Blobs.AsNoTracking();
}
