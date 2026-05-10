using Granit.Browsing;
using Granit.Documents.Renditions.Pdf.Extensions;
using Granit.Modularity;

namespace Granit.Documents.Renditions.Pdf;

/// <summary>
/// Granit module wiring the <c>application/pdf → image/png</c> rendition provider
/// (F16.6). Chains naturally with <c>Granit.Documents.Renditions.Imaging</c> when the
/// caller requests JPEG / WebP / AVIF — the pipeline solver builds
/// <c>pdf → png → image/*</c> in two hops.
/// </summary>
/// <remarks>
/// Soft-depends on <see cref="GranitBrowsingModule"/>; a Chromium-backed provider
/// (<c>Granit.Browsing.PuppeteerSharp</c> or <c>Granit.Browsing.Playwright</c>) MUST
/// be wired by the host. Failure is reported at startup, not on first render.
/// </remarks>
[DependsOn(
    typeof(GranitBrowsingModule),
    typeof(GranitDocumentsRenditionsModule))]
public sealed class GranitDocumentsRenditionsPdfModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsRenditionsPdf();
    }
}
