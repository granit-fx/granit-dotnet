using Granit.BlobStorage.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IBlobQueryableProvider"/>.
/// Exposes <see cref="IQueryable{T}"/> access to blob entities via <see cref="BlobStorageDbContext"/>.
/// </summary>
internal sealed class EfBlobQueryableProvider(IDbContextFactory<BlobStorageDbContext> contextFactory)
    : IBlobQueryableProvider
{
    private readonly BlobStorageDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<BlobDescriptor> GetDescriptors() =>
        _context.Blobs.AsNoTracking();
}
