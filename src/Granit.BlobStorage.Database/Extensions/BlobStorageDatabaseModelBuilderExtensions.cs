using Granit.BlobStorage.Database.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.Database.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit BlobStorage Database entity
/// configurations in a tenant-owned <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// Call <see cref="ConfigureBlobStorageDatabaseModule"/> inside the tenant's
/// <c>OnModelCreating</c> so that EF Core migrations include the blob content tables.
/// </remarks>
public static class BlobStorageDatabaseModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit BlobStorage Database module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureBlobStorageDatabaseModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DatabaseBlobContentConfiguration());
        return modelBuilder;
    }
}
