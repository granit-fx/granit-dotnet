using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.Renditions.Wolverine;

/// <summary>
/// Resolves the bytes of a source <c>DocumentVersion</c> blob so the rendition pipeline
/// can consume them. Abstracted because <c>IBlobStorage</c> exposes only presigned URLs —
/// the default implementation fetches via <c>HttpClient</c>; tests substitute an in-memory
/// implementation.
/// </summary>
public interface IRenditionSourceFetcher
{
    /// <summary>
    /// Returns a readable stream over the blob bytes. Callers dispose the stream.
    /// </summary>
    /// <param name="blobDescriptorId">Blob to fetch (lives in the documents container).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Stream> OpenSourceAsync(Guid blobDescriptorId, CancellationToken cancellationToken = default);
}
