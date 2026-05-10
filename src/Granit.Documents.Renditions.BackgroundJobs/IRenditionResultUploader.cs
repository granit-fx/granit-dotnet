using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.Renditions.BackgroundJobs;

/// <summary>
/// Persists the bytes emitted by <c>IRenditionPipeline</c> into the
/// <c>document-renditions</c> blob container. Returns the resulting
/// <c>BlobDescriptorId</c> the caller stores on the <c>DocumentRendition</c> row.
/// </summary>
public interface IRenditionResultUploader
{
    /// <summary>Uploads the bytes and returns the resulting blob descriptor id.</summary>
    Task<Guid> UploadAsync(
        RenditionResult result,
        CancellationToken cancellationToken = default);
}
