using System.ComponentModel.DataAnnotations;

namespace Granit.DocumentGeneration.Pdf.Options;

/// <summary>
/// Options for the HTML → PDF renderer. Bound from configuration section
/// <c>DocumentGeneration:Pdf</c>.
/// </summary>
/// <remarks>
/// Browser-pool sizing, Chromium executable path, and sandbox flags are owned by the
/// <c>Granit.Browsing</c> provider configuration (<c>Browsing:</c> +
/// <c>Browsing:PuppeteerSharp</c> / <c>Browsing:Playwright</c>) — keep this surface
/// scoped to render-output concerns.
/// </remarks>
public sealed class PdfRenderOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DocumentGeneration:Pdf";

    /// <summary>
    /// Paper format (e.g. <c>"A4"</c>, <c>"A5"</c>, <c>"Letter"</c>). Default <c>"A4"</c>.
    /// </summary>
    [Required]
    public string PaperFormat { get; set; } = "A4";

    /// <summary>Whether to use landscape orientation. Default <see langword="false"/> (portrait).</summary>
    public bool Landscape { get; set; }

    /// <summary>Top margin in CSS units (e.g. <c>"10mm"</c>, <c>"1cm"</c>). Default <c>"10mm"</c>.</summary>
    public string MarginTop { get; set; } = "10mm";

    /// <summary>Bottom margin in CSS units. Default <c>"10mm"</c>.</summary>
    public string MarginBottom { get; set; } = "10mm";

    /// <summary>Left margin in CSS units. Default <c>"10mm"</c>.</summary>
    public string MarginLeft { get; set; } = "10mm";

    /// <summary>Right margin in CSS units. Default <c>"10mm"</c>.</summary>
    public string MarginRight { get; set; } = "10mm";

    /// <summary>
    /// HTML template for the page header. Supports the standard Chromium variables:
    /// <c>date</c>, <c>title</c>, <c>url</c>, <c>pageNumber</c>, <c>totalPages</c>.
    /// </summary>
    public string? HeaderTemplate { get; set; }

    /// <summary>
    /// HTML template for the page footer. Default shows page numbers
    /// (<c>Page x / y</c>).
    /// </summary>
    public string FooterTemplate { get; set; } =
        """<div style="font-size:9px; width:100%; text-align:center; color:#999; padding:0 10mm;">Page <span class="pageNumber"></span> / <span class="totalPages"></span></div>""";

    /// <summary>Whether to print background graphics. Default <see langword="true"/>.</summary>
    public bool PrintBackground { get; set; } = true;

    /// <summary>
    /// Maximum time in milliseconds for page rendering and PDF generation. Default
    /// <c>30000</c> (30 seconds).
    /// </summary>
    [Range(1_000, 300_000)]
    public int RenderTimeoutMs { get; set; } = 30_000;
}
