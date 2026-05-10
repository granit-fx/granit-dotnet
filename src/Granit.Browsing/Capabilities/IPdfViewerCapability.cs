using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Browsing.Capabilities;

/// <summary>
/// Loads a PDF document inside the engine's native PDF viewer and exposes per-page
/// rendering. Chromium-only — uses the built-in PDF viewer extension. Consumers (e.g.
/// <c>Granit.Documents.Renditions.Pdf</c>) generate thumbnail / preview images for PDF
/// uploads without bundling a separate PDFium / SkiaSharp stack.
/// </summary>
public interface IPdfViewerCapability
{
    /// <summary>
    /// Loads the PDF in the supplied page's native viewer and returns a handle that
    /// exposes page count and per-page rendering. Disposing the handle releases the
    /// resources held by the viewer.
    /// </summary>
    /// <param name="page">Browser page used as the host for the viewer. The caller MUST NOT navigate the page while the returned handle is alive.</param>
    /// <param name="pdf">PDF bytes. The stream is read once and may be consumed entirely by the implementation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IPdfDocumentPage> OpenPdfAsync(IBrowserPage page, Stream pdf, CancellationToken cancellationToken = default);
}

/// <summary>Handle on a PDF loaded in a browser-native viewer.</summary>
public interface IPdfDocumentPage : IAsyncDisposable
{
    /// <summary>Total number of pages in the loaded PDF.</summary>
    int PageCount { get; }

    /// <summary>
    /// Renders the page at <paramref name="pageIndex"/> (zero-based) to an image. The
    /// implementation chooses an appropriate raster size based on
    /// <paramref name="dimensions"/>; callers wanting an exact size should re-encode
    /// downstream through <c>Granit.Imaging</c>.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="dimensions">Target raster dimensions in pixels.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PNG bytes by default — providers MAY honor a preferred format hint via their own options surface.</returns>
    Task<byte[]> RenderPageToImageAsync(int pageIndex, RenditionDimensions dimensions, CancellationToken cancellationToken = default);
}

/// <summary>Pixel dimensions for a rendered raster.</summary>
/// <param name="Width">Width in pixels — must be strictly positive.</param>
/// <param name="Height">Height in pixels — must be strictly positive.</param>
public sealed record RenditionDimensions(int Width, int Height);
