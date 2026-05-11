using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.AssetMetadata.BackgroundJobs;

/// <summary>
/// Resolves the bytes of a source <c>DocumentVersion</c> blob so the metadata
/// extractor chain can consume them. Abstracted because <c>IBlobStorage</c>
/// exposes only presigned URLs — the default implementation fetches via
/// <c>HttpClient</c>; tests substitute an in-memory implementation.
/// Mirrors <c>Granit.Documents.Renditions.BackgroundJobs.IRenditionSourceFetcher</c>
/// — the two pipelines live side-by-side and we keep the abstractions per-package
/// to avoid a cross-dependency between renditions and asset metadata.
/// </summary>
public interface IAssetMetadataSourceFetcher
{
    /// <summary>
    /// Returns a readable stream over the blob bytes. Callers dispose the stream.
    /// </summary>
    Task<Stream> OpenSourceAsync(Guid blobDescriptorId, CancellationToken cancellationToken = default);
}
