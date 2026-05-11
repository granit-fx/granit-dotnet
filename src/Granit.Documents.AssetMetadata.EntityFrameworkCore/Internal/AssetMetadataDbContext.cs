using Granit.DataFiltering;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.AssetMetadata.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for <c>Granit.Documents.AssetMetadata</c>. Isolated
/// from <c>DocumentsDbContext</c> so the asset-metadata module can ship its own
/// migrations and be replaced wholesale by hosts that prefer an alternate storage
/// backend.
/// </summary>
internal sealed class AssetMetadataDbContext(
    DbContextOptions<AssetMetadataDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Asset-metadata rows, one per <c>DocumentVersion</c>.</summary>
    public DbSet<DocumentAssetMetadata> AssetMetadata { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureAssetMetadataModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
