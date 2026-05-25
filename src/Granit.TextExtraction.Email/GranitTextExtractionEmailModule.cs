using Granit.Html.AngleSharp;
using Granit.Modularity;
using Granit.TextExtraction.Email.Extensions;

namespace Granit.TextExtraction.Email;

/// <summary>
/// Granit module that registers <see cref="EmailTextExtractor"/> with the
/// <c>Granit.TextExtraction</c> pipeline. Depends on
/// <see cref="GranitHtmlAngleSharpModule"/> so the SSRF-safe HTML→plain-text
/// converter is available for the HTML body fallback path.
/// </summary>
[DependsOn(typeof(GranitHtmlAngleSharpModule), typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionEmailModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTextExtractionEmail();
}
