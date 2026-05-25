using Granit.Modularity;
using Granit.TextExtraction.Pdf.Extensions;

namespace Granit.TextExtraction.Pdf;

/// <summary>
/// Granit module that registers <see cref="PdfTextExtractor"/> with the
/// <c>Granit.TextExtraction</c> pipeline.
/// </summary>
[DependsOn(typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionPdfModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTextExtractionPdf();
}
