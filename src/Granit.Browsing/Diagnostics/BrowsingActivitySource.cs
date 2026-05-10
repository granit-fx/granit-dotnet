using System.Diagnostics;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Browsing distributed tracing.
/// </summary>
/// <remarks>
/// Registered via <c>GranitActivitySourceRegistry</c> by <c>GranitBrowsingModule</c>;
/// <c>Granit.Observability</c> picks it up automatically. Internal — provider packages
/// see it through <c>InternalsVisibleTo</c>; external consumers subscribe by
/// <see cref="Name"/>.
/// </remarks>
internal static class BrowsingActivitySource
{
    /// <summary>The name of the Granit.Browsing <see cref="ActivitySource"/>.</summary>
    public const string Name = "Granit.Browsing";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    public static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────────────────────────────────────────────────────
    /// <summary>Acquire a page from the pool.</summary>
    public const string PageAcquire = "browsing.page.acquire";

    /// <summary>Navigate a page.</summary>
    public const string PageNavigate = "browsing.page.navigate";

    /// <summary>Set HTML content on a page.</summary>
    public const string PageSetContent = "browsing.page.set_content";

    /// <summary>Capture a screenshot.</summary>
    public const string Screenshot = "browsing.screenshot";

    /// <summary>Render a PDF (Chromium-only).</summary>
    public const string PdfRender = "browsing.pdf.render";

    /// <summary>Open a PDF in the native viewer (Chromium-only).</summary>
    public const string PdfViewerOpen = "browsing.pdf_viewer.open";

    /// <summary>Render a single PDF page from the native viewer.</summary>
    public const string PdfViewerRenderPage = "browsing.pdf_viewer.render_page";
}
