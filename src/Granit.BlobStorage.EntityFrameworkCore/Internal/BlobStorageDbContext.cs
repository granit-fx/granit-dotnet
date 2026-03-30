using Granit.BlobStorage.Domain;
using Granit.BlobStorage.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit blob storage descriptors.
/// </summary>
/// <remarks>
/// <para>
/// Isolated from the host application's DbContext to avoid coupling. Stores only the
/// <see cref="BlobDescriptor"/> audit record — the binary content lives on S3.
/// </para>
/// <para>
/// Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class BlobStorageDbContext(
    DbContextOptions<BlobStorageDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Lifecycle records for all uploaded blobs.</summary>
    public DbSet<BlobDescriptor> Blobs { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureBlobStorageModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
