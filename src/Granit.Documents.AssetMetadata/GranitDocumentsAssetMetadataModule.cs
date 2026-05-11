using Granit.Diagnostics;
using Granit.Documents;
using Granit.Documents.AssetMetadata.Diagnostics;
using Granit.Documents.AssetMetadata.Extensions;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata;

/// <summary>
/// Granit module for the asset-metadata abstraction layer (F17.1).
/// </summary>
/// <remarks>
/// Anchor module: registers <c>AssetMetadataMetrics</c> + the
/// <c>Granit.Documents.AssetMetadata</c> <see cref="System.Diagnostics.ActivitySource"/>
/// and the pipeline. Extractor packages (<c>Granit.Documents.AssetMetadata.Imaging</c>,
/// <c>.Pdf</c>, <c>.Office</c>, <c>.Media</c>) and the storage companion
/// (<c>.EntityFrameworkCore</c>) wire themselves on top via their own extension methods.
/// </remarks>
[DependsOn(typeof(GranitDocumentsModule))]
public sealed class GranitDocumentsAssetMetadataModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(AssetMetadataActivitySource.Name);
        context.Services.AddGranitDocumentsAssetMetadata();
    }
}
