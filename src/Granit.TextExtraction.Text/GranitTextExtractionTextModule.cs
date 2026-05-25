using Granit.Html.AngleSharp;
using Granit.Modularity;
using Granit.TextExtraction.Text.Extensions;

namespace Granit.TextExtraction.Text;

/// <summary>
/// Granit module that registers the HTML and Markdown text extractors with the
/// <c>Granit.TextExtraction</c> pipeline. Depends on
/// <see cref="GranitHtmlAngleSharpModule"/> so the keyed
/// <c>HtmlConverterKeys.Untrusted</c> <c>IHtmlToPlainTextConverter</c> is available
/// for <see cref="HtmlTextExtractor"/>.
/// </summary>
[DependsOn(typeof(GranitHtmlAngleSharpModule), typeof(GranitTextExtractionModule))]
public sealed class GranitTextExtractionTextModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTextExtractionText();
}
