namespace Granit.TextExtraction.Pdf.Ocr.Options;

/// <summary>
/// Configuration for the scanned-PDF OCR extractor. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class PdfOcrOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TextExtraction:Pdf:Ocr";

    /// <summary>
    /// Minimum number of characters PdfPig must return for a page to be considered
    /// "already textual". Below this threshold the page is treated as scanned and
    /// handed to the OCR backend. The default of 32 catches truly empty pages plus
    /// the common scan-only-output case where a page yields a stray header character
    /// or two from a watermark.
    /// </summary>
    public int MinNativeCharsPerPage { get; set; } = 32;

    /// <summary>
    /// Rendering resolution in dots per inch for each rasterised page. 300 DPI matches
    /// the de-facto OCR sweet spot (Tesseract recommends 300–600). Lower to trade
    /// accuracy for throughput; higher to chase small-glyph quality at the cost of
    /// memory (an A4 page at 600 DPI is ~140 MB uncompressed).
    /// </summary>
    public int RenderDpi { get; set; } = 300;

    /// <summary>
    /// Hard cap on the number of pages the extractor will rasterise per document.
    /// Documents with more pages truncate gracefully (the rest are dropped, the
    /// result is flagged <see cref="TextExtractionResult.IsTruncated"/>=true).
    /// Defaults to 200 — high enough for real-world scanned reports without letting
    /// a multi-thousand-page PDF burn a worker forever.
    /// </summary>
    public int MaxPagesToRasterise { get; set; } = 200;

    /// <summary>
    /// Maximum decoded image surface (width × height) for a single rasterised page.
    /// Pages whose computed dimensions exceed this are skipped (no rasterisation, no
    /// OCR call) — defence-in-depth against PDFs that declare 100k×100k page sizes.
    /// Defaults to 100 MP — covers A0 @ 300 DPI with margin.
    /// </summary>
    public long MaxPagePixels { get; set; } = 100L * 1024 * 1024;
}
