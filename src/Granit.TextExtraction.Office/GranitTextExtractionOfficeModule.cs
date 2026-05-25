using Granit.Modularity;
using Granit.TextExtraction.Office.Extensions;

namespace Granit.TextExtraction.Office;

/// <summary>
/// Granit module that registers <see cref="WordTextExtractor"/>,
/// <see cref="ExcelTextExtractor"/>, and <see cref="PowerPointTextExtractor"/> with the
/// <c>Granit.TextExtraction</c> pipeline.
/// </summary>
[DependsOn(typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionOfficeModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTextExtractionOffice();
}
