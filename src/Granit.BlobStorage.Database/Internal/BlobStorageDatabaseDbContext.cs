using Granit.BlobStorage.Database.Domain;
using Granit.BlobStorage.Database.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.Database.Internal;

/// <summary>
/// Isolated <see cref="DbContext"/> for blob content storage.
/// Separate from <c>BlobStorageDbContext</c> (which stores descriptors).
/// </summary>
internal sealed class BlobStorageDatabaseDbContext(
    DbContextOptions<BlobStorageDatabaseDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    public DbSet<DatabaseBlobContent> BlobContents => Set<DatabaseBlobContent>();

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureBlobStorageDatabaseModule();
}
