using Granit.Documents.AssetMetadata.Imaging.Extensions;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata.Imaging;

/// <summary>
/// Granit module registering <see cref="Internal.ImageMetadataExtractor"/>
/// on top of <c>Granit.Documents.AssetMetadata</c>. Handles every <c>image/*</c>
/// MIME and projects EXIF / IPTC / XMP into the typed columns plus a verbatim
/// raw dump for forensics.
/// </summary>
[DependsOn(typeof(GranitDocumentsAssetMetadataModule))]
public sealed class GranitDocumentsAssetMetadataImagingModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsAssetMetadataImaging();
    }
}
