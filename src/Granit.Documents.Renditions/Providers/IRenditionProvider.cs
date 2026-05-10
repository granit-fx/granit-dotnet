using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.Renditions.Providers;

/// <summary>
/// A single transformation step in a rendition pipeline. Providers declare which input
/// MIME types they accept (<see cref="CanHandle"/>) and what output type they produce
/// (<see cref="OutputContentType"/>); the pipeline solver chains them together to bridge
/// arbitrary <c>(sourceContentType, targetContentType)</c> pairs.
/// </summary>
/// <remarks>
/// <para>
/// Examples shipped by the framework:
/// </para>
/// <list type="bullet">
///   <item><c>ImagingRenditionProvider</c> — <c>image/* → image/*</c> (resize / re-encode)</item>
///   <item><c>PdfRenditionProvider</c> — <c>application/pdf → image/png</c></item>
///   <item><c>OfficeRenditionProvider</c> — <c>docx/xlsx/pptx → application/pdf</c></item>
/// </list>
/// <para>
/// Office → image is reached by chaining the Office provider into the Pdf provider. The
/// pipeline solver finds shortest paths (BFS) up to a configured maximum chain length.
/// </para>
/// <para>
/// Providers are registered as singletons. <see cref="GenerateAsync"/> implementations
/// MUST be safe to call concurrently; out-of-process providers (Office / video) typically
/// own a process pool internally.
/// </para>
/// </remarks>
public interface IRenditionProvider
{
    /// <summary>Stable name used in diagnostics and tracing tags (e.g. <c>"imaging"</c>, <c>"pdf-viewer"</c>, <c>"office-libre"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Returns <c>true</c> when this provider can transform <paramref name="sourceContentType"/>
    /// (e.g. <c>"application/pdf"</c>) into <see cref="OutputContentType"/> directly.
    /// </summary>
    bool CanHandle(string sourceContentType);

    /// <summary>
    /// MIME type produced when this provider runs. The pipeline solver uses this to
    /// build the chain — a provider that handles <c>image/*</c> and produces
    /// <c>image/*</c> reports an output token like <c>"image/*"</c> and may coerce the
    /// final encoding from <see cref="RenditionTarget.TargetContentType"/>.
    /// </summary>
    string OutputContentType { get; }

    /// <summary>
    /// Runs the transformation. Implementations consume <paramref name="source"/>
    /// (the stream is positioned at the start) and produce a fresh
    /// <see cref="RenditionResult"/>; the source stream is NOT closed by the provider.
    /// </summary>
    /// <param name="source">Bytes of the upstream content (raw upload for the first hop, intermediate result for chained hops).</param>
    /// <param name="sourceContentType">MIME type of <paramref name="source"/>.</param>
    /// <param name="target">Target description (kind, MIME, dimensions, quality).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RenditionResult> GenerateAsync(
        Stream source,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default);
}
