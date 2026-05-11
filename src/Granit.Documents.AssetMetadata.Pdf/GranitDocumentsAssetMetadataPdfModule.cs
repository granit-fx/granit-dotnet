using Granit.Documents.AssetMetadata.Pdf.Extensions;
using Granit.Modularity;

namespace Granit.Documents.AssetMetadata.Pdf;

/// <summary>
/// Granit module registering <see cref="Internal.PdfMetadataExtractor"/>
/// on top of <c>Granit.Documents.AssetMetadata</c>. Handles <c>application/pdf</c>
/// sources and projects the PDF <c>Information</c> dictionary into the typed
/// columns plus a verbatim raw dump for forensics.
/// </summary>
[DependsOn(typeof(GranitDocumentsAssetMetadataModule))]
public sealed class GranitDocumentsAssetMetadataPdfModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsAssetMetadataPdf();
    }
}
