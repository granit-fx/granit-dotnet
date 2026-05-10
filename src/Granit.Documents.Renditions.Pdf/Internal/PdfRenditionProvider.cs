using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Browsing;
using Granit.Browsing.Capabilities;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.Providers;

namespace Granit.Documents.Renditions.Pdf.Internal;

/// <summary>
/// <c>application/pdf → image/png</c> rendition provider. Renders the requested PDF page
/// through <see cref="IPdfViewerCapability"/> exposed by a Chromium-backed
/// <see cref="IHeadlessBrowser"/>; downstream conversion to other image formats happens
/// by chaining with the Imaging provider (the pipeline solver resolves
/// <c>pdf → png → webp</c> automatically).
/// </summary>
/// <remarks>
/// The provider always emits PNG; the pipeline solver inspects
/// <see cref="OutputContentType"/> when building chains.
/// </remarks>
internal sealed class PdfRenditionProvider(
    IHeadlessBrowser browser,
    IPdfViewerCapability pdfViewer) : IRenditionProvider
{
    /// <inheritdoc />
    public string Name => "pdf-viewer";

    /// <inheritdoc />
    public string OutputContentType => "image/png";

    /// <inheritdoc />
    public bool CanHandle(string sourceContentType) =>
        string.Equals(sourceContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<RenditionResult> GenerateAsync(
        Stream source,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        Granit.Documents.Renditions.RenditionDimensions dims =
            target.Dimensions ?? new Granit.Documents.Renditions.RenditionDimensions(800, 1132);
        var browsingDims = new global::Granit.Browsing.Capabilities.RenditionDimensions(dims.Width, dims.Height);

        await using IBrowserPage page = await browser
            .AcquirePageAsync(options: null, cancellationToken)
            .ConfigureAwait(false);
        await using IPdfDocumentPage pdf = await pdfViewer
            .OpenPdfAsync(page, source, cancellationToken)
            .ConfigureAwait(false);

        byte[] pngBytes = await pdf
            .RenderPageToImageAsync(pageIndex: 0, browsingDims, cancellationToken)
            .ConfigureAwait(false);

        return new RenditionResult(pngBytes, OutputContentType, dims.Width, dims.Height);
    }
}
