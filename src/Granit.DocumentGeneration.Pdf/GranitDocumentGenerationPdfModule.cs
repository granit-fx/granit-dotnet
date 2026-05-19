using Granit.Browsing;
using Granit.DocumentGeneration.Pdf.Extensions;
using Granit.Modularity;

namespace Granit.DocumentGeneration.Pdf;

/// <summary>
/// Granit module that registers the HTML → PDF renderer on top of
/// <c>Granit.Browsing</c>.
/// </summary>
/// <remarks>
/// Composes with <see cref="GranitBrowsingModule"/> for the abstraction; a concrete
/// provider module (<c>Granit.Browsing.PuppeteerSharp</c> or
/// <c>Granit.Browsing.Playwright</c>) MUST also be loaded by the host so that
/// <c>IHeadlessBrowser</c> + <c>IPdfCapability</c> resolve.
/// </remarks>
[DependsOn(typeof(GranitBrowsingModule), typeof(GranitDocumentGenerationModule))]
public sealed class GranitDocumentGenerationPdfModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDocumentGenerationPdf();
}
