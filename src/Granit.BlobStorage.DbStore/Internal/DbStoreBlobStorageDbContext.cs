using Granit.BlobStorage.DbStore.Configurations;
using Granit.BlobStorage.DbStore.Entities;
using Granit.Core.DataFiltering;
using Granit.Core.MultiTenancy;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.DbStore.Internal;

/// <summary>
/// Isolated <see cref="DbContext"/> for blob content storage.
/// Separate from <c>BlobStorageDbContext</c> (which stores descriptors).
/// </summary>
internal sealed class DbStoreBlobStorageDbContext(
    DbContextOptions<DbStoreBlobStorageDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<DbStoreBlobContent> BlobContents => Set<DbStoreBlobContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new DbStoreBlobContentConfiguration());
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
