using Granit.Modularity;
using Granit.TextExtraction.Extensions;

namespace Granit.TextExtraction;

/// <summary>
/// Granit module for byte-to-text extraction.
/// </summary>
/// <remarks>
/// Registers the pipeline, the plain-text fallback, options, metrics, and the activity
/// source. Concrete extractors (PDF, Office, HTML, OCR, Tika, VLM) plug in through their
/// own packages.
/// <para>
/// Localization resources (<c>Localization/TextExtraction/{culture}.json</c>) are embedded
/// in this assembly and auto-discovered by <c>GranitLocalizationModule</c> via
/// <see cref="TextExtractionLocalizationResource"/>.
/// </para>
/// </remarks>
public sealed class GranitTextExtractionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTextExtraction();
}
