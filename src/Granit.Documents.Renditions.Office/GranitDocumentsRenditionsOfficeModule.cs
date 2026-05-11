using Granit.Documents.Renditions.Office.Extensions;
using Granit.Modularity;

namespace Granit.Documents.Renditions.Office;

/// <summary>
/// Granit module wiring the <c>Office → application/pdf</c> rendition provider
/// (F16.7). Chains naturally with <c>Granit.Documents.Renditions.Pdf</c> +
/// <c>Granit.Documents.Renditions.Imaging</c> to reach an image target —
/// <c>docx → pdf → png → webp</c> resolves in 3 hops within the default
/// <c>MaxChainLength</c>.
/// </summary>
/// <remarks>
/// Requires LibreOffice headless (<c>soffice</c>) on the runtime image. The
/// framework does not bundle the binary — hosts install it via their base image
/// (Debian: <c>apt-get install libreoffice</c>; Alpine: <c>apk add libreoffice</c>;
/// macOS: <c>brew install --cask libreoffice</c>).
/// </remarks>
[DependsOn(typeof(GranitDocumentsRenditionsModule))]
public sealed class GranitDocumentsRenditionsOfficeModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitDocumentsRenditionsOffice();
    }
}
