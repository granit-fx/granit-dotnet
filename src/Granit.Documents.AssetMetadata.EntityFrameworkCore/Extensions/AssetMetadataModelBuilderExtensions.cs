using Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including
/// <c>Granit.Documents.AssetMetadata</c> entity configurations in a host-owned
/// <see cref="DbContext"/>.
/// </summary>
public static class AssetMetadataModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the asset-metadata module.</summary>
    public static ModelBuilder ConfigureAssetMetadataModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new DocumentAssetMetadataConfiguration());
        return modelBuilder;
    }
}
