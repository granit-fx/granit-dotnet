namespace Granit.Browsing.Capabilities;

/// <summary>Options for <see cref="IPdfCapability.RenderToPdfAsync"/>.</summary>
public sealed record PdfOptions
{
    /// <summary>Paper format. Mutually exclusive with <see cref="Width"/> / <see cref="Height"/>.</summary>
    public PaperFormat? Format { get; init; } = PaperFormat.A4;

    /// <summary>Custom paper width (CSS units, e.g. <c>"210mm"</c>). Pair with <see cref="Height"/>.</summary>
    public string? Width { get; init; }

    /// <summary>Custom paper height (CSS units, e.g. <c>"297mm"</c>). Pair with <see cref="Width"/>.</summary>
    public string? Height { get; init; }

    /// <summary>Landscape orientation when <c>true</c>.</summary>
    public bool Landscape { get; init; }

    /// <summary>Page margins. <c>null</c> uses zero margins.</summary>
    public PdfMargins? Margins { get; init; }

    /// <summary>When <c>true</c>, prints background graphics. Mirrors the Chromium <c>print-background</c> flag.</summary>
    public bool PrintBackground { get; init; } = true;

    /// <summary>Display header / footer? When <c>true</c>, supplied templates are rendered.</summary>
    public bool DisplayHeaderFooter { get; init; }

    /// <summary>HTML template for the page header. Supports template variables — see provider docs.</summary>
    public string? HeaderTemplate { get; init; }

    /// <summary>HTML template for the page footer.</summary>
    public string? FooterTemplate { get; init; }

    /// <summary>Page range expression (<c>"1-3,5"</c>). <c>null</c> prints the whole document.</summary>
    public string? PageRanges { get; init; }

    /// <summary>Render scale in [0.1, 2.0]. Defaults to 1.0.</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>When <c>true</c>, prefers the page's CSS-defined size over <see cref="Format"/> / <see cref="Width"/>.</summary>
    public bool PreferCssPageSize { get; init; }
}

/// <summary>Page margins for a generated PDF.</summary>
/// <param name="Top">CSS unit, e.g. <c>"10mm"</c>.</param>
/// <param name="Right">CSS unit.</param>
/// <param name="Bottom">CSS unit.</param>
/// <param name="Left">CSS unit.</param>
public sealed record PdfMargins(string? Top = null, string? Right = null, string? Bottom = null, string? Left = null);

/// <summary>Standard paper formats.</summary>
public enum PaperFormat
{
    /// <summary>A4 (210 × 297 mm) — default.</summary>
    A4,

    /// <summary>A3 (297 × 420 mm).</summary>
    A3,

    /// <summary>A5 (148 × 210 mm).</summary>
    A5,

    /// <summary>US Letter (8.5 × 11 in).</summary>
    Letter,

    /// <summary>US Legal (8.5 × 14 in).</summary>
    Legal,

    /// <summary>US Tabloid (11 × 17 in).</summary>
    Tabloid,
}
