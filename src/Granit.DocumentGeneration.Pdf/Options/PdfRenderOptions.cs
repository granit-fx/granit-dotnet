using System.ComponentModel.DataAnnotations;

namespace Granit.DocumentGeneration.Pdf.Options;

/// <summary>
/// Options for the PuppeteerSharp PDF renderer.
/// Bound from configuration section <c>DocumentGeneration:Pdf</c>.
/// </summary>
public sealed class PdfRenderOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DocumentGeneration:Pdf";

    /// <summary>
    /// Paper format (e.g. "A4", "A5", "Letter"). Default is "A4".
    /// </summary>
    [Required]
    public string PaperFormat { get; set; } = "A4";

    /// <summary>
    /// Whether to use landscape orientation. Default is <see langword="false"/> (portrait).
    /// </summary>
    public bool Landscape { get; set; }

    /// <summary>
    /// Top margin in CSS units (e.g. "10mm", "1cm"). Default is "10mm".
    /// </summary>
    public string MarginTop { get; set; } = "10mm";

    /// <summary>
    /// Bottom margin in CSS units. Default is "10mm".
    /// </summary>
    public string MarginBottom { get; set; } = "10mm";

    /// <summary>
    /// Left margin in CSS units. Default is "10mm".
    /// </summary>
    public string MarginLeft { get; set; } = "10mm";

    /// <summary>
    /// Right margin in CSS units. Default is "10mm".
    /// </summary>
    public string MarginRight { get; set; } = "10mm";

    /// <summary>
    /// HTML template for the page header. Supports PuppeteerSharp classes:
    /// <c>date</c>, <c>title</c>, <c>url</c>, <c>pageNumber</c>, <c>totalPages</c>.
    /// </summary>
    public string? HeaderTemplate { get; set; }

    /// <summary>
    /// HTML template for the page footer. Supports the same classes as <see cref="HeaderTemplate"/>.
    /// Default shows page numbers (Page x of y).
    /// </summary>
    public string FooterTemplate { get; set; } =
        """<div style="font-size:9px; width:100%; text-align:center; color:#999; padding:0 10mm;">Page <span class="pageNumber"></span> / <span class="totalPages"></span></div>""";

    /// <summary>
    /// Whether to print background graphics. Default is <see langword="true"/>.
    /// </summary>
    public bool PrintBackground { get; set; } = true;

    /// <summary>
    /// Path to a custom Chromium executable. If <see langword="null"/>,
    /// PuppeteerSharp downloads and manages its own Chromium instance.
    /// </summary>
    public string? ChromiumExecutablePath { get; set; }

    /// <summary>
    /// Maximum time in milliseconds for page rendering and PDF generation.
    /// Default is <c>30000</c> (30 seconds).
    /// </summary>
    [Range(1_000, 300_000)]
    public int RenderTimeoutMs { get; set; } = 30_000;

    /// <summary>
    /// Maximum number of concurrent Chromium pages (tabs) for parallel rendering.
    /// Default is <c>4</c>.
    /// </summary>
    [Range(1, 32)]
    public int MaxConcurrentPages { get; set; } = 4;

    /// <summary>
    /// Disables the Chromium OS-level process sandbox. Only enable in containerized
    /// environments that cannot grant <c>CAP_SYS_ADMIN</c> or configure a
    /// <c>seccomp</c> profile for Chromium. Default is <see langword="false"/>.
    /// </summary>
    public bool DisableSandbox { get; set; }
}
