using Granit.Modularity;
using Granit.TextExtraction.Text.Extensions;

namespace Granit.TextExtraction.Text;

/// <summary>
/// Granit module that registers the HTML and Markdown text extractors with the
/// <c>Granit.TextExtraction</c> pipeline.
/// </summary>
[DependsOn(typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionTextModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTextExtractionText();
}
