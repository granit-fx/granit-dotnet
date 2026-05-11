using Granit.Documents.AssetMetadata.Office.Extensions;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata.Office;

/// <summary>
/// Granit module registering <see cref="Internal.OfficeMetadataExtractor"/>
/// on top of <c>Granit.Documents.AssetMetadata</c>. Handles OOXML
/// (<c>.docx</c> / <c>.xlsx</c> / <c>.pptx</c>) sources and projects the
/// core + extended properties into the typed columns plus a verbatim raw
/// dump for forensics.
/// </summary>
[DependsOn(typeof(GranitDocumentsAssetMetadataModule))]
public sealed class GranitDocumentsAssetMetadataOfficeModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsAssetMetadataOffice();
    }
}
