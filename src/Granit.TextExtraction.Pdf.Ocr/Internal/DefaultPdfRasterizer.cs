using System.Runtime.Versioning;
using PDFtoImage;

namespace Granit.TextExtraction.Pdf.Ocr.Internal;

/// <summary>
/// Default <see cref="IPdfRasterizer"/> backed by PDFtoImage (PDFium + SkiaSharp).
/// </summary>
/// <remarks>
/// <para>
/// PDFium's C++ engine is not thread-safe at the document level — the wrapper itself
/// serialises internally, but we add our own gate to keep the contract explicit and
/// to avoid a thundering-herd of pages queuing inside PDFtoImage's internal lock.
/// </para>
/// <para>
/// Page-streaming: each call renders ONE page to a fresh <see cref="MemoryStream"/>;
/// the bitmap is not held beyond the PNG encode. A 1000-page document never holds
/// more than one decoded page in memory at a time.
/// </para>
/// </remarks>
[SupportedOSPlatform("Linux")]
[SupportedOSPlatform("Windows")]
[SupportedOSPlatform("macOS")]
[SupportedOSPlatform("Android31.0")]
[SupportedOSPlatform("iOS13.6")]
[SupportedOSPlatform("MacCatalyst13.5")]
internal sealed class DefaultPdfRasterizer : IPdfRasterizer
{
    private readonly Lock _gate = new();

    public Task<byte[]> RasterisePageAsync(
        ReadOnlyMemory<byte> pdfBytes,
        int pageIndex,
        int dpi,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PDFium is synchronous + holds native state; do the work on the calling thread
        // under the lock. Cancellation can't interrupt the native render mid-page, but
        // honouring it on entry keeps a cancelled batch from queuing more pages.
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] pdf = pdfBytes.ToArray();
            using MemoryStream png = new();
            Conversion.SavePng(
                png,
                pdf,
                page: new Index(pageIndex),
                options: new RenderOptions(Dpi: dpi));

            return Task.FromResult(png.ToArray());
        }
    }
}
