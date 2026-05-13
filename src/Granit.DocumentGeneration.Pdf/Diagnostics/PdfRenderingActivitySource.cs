using System.Diagnostics;

namespace Granit.DocumentGeneration.Pdf.Diagnostics;

/// <summary>
/// <see cref="ActivitySource"/> for the PDF rendering pipeline. Registered with
/// <c>GranitActivitySourceRegistry</c> on module load.
/// </summary>
internal static class PdfRenderingActivitySource
{
    /// <summary>Name of the <see cref="ActivitySource"/>.</summary>
    public const string Name = "Granit.DocumentGeneration.Pdf";

    /// <summary>Operation name for the full HTML → PDF render.</summary>
    public const string RenderPdf = "PdfRendering.Render";

    public static readonly ActivitySource Source = new(Name);
}
