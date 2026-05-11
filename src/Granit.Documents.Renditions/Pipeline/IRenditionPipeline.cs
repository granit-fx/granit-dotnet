using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Exceptions;

namespace Granit.Documents.Renditions.Pipeline;

/// <summary>
/// Resolves a chain of <c>IRenditionProvider</c> instances bridging a
/// <paramref name="sourceContentType"/> to a <c>RenditionTarget.TargetContentType</c>,
/// and runs them sequentially. Hosts inject this rather than individual providers.
/// </summary>
public interface IRenditionPipeline
{
    /// <summary>
    /// Generates a rendition for <paramref name="source"/>. Throws
    /// <see cref="RenditionPipelineException"/> when no provider chain bridges the
    /// supplied content types within the configured maximum chain length.
    /// </summary>
    Task<RenditionResult> ExecuteAsync(
        Stream source,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the pipeline can bridge
    /// <paramref name="sourceContentType"/> to <paramref name="targetContentType"/>.
    /// Useful for upload-time policy checks ("should we even create a Pending row for
    /// this rendition?").
    /// </summary>
    bool CanBridge(string sourceContentType, string targetContentType);
}
