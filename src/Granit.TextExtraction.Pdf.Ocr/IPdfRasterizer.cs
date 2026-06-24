namespace Granit.TextExtraction.Pdf.Ocr;

/// <summary>
/// Renders single PDF pages to raster PNG bytes. Page-streaming by contract — the
/// extractor only ever holds one page bitmap in memory at a time, so a 1000-page scan
/// can't blow the LOH. Implementations MUST be safe to invoke concurrently from
/// multiple callers; the default impl pins access via a lock around the underlying
/// PDFium engine (PDFium is not thread-safe).
/// </summary>
public interface IPdfRasterizer
{
    /// <summary>
    /// Renders a single page (zero-based index) at <paramref name="dpi"/> and returns
    /// the encoded PNG bytes. Throws on PDF-level errors (corrupt page, missing page);
    /// the calling extractor catches and soft-skips so a single bad page never sinks
    /// the whole document.
    /// </summary>
    Task<byte[]> RasterisePageAsync(
        ReadOnlyMemory<byte> pdfBytes,
        int pageIndex,
        int dpi,
        CancellationToken cancellationToken);
}
