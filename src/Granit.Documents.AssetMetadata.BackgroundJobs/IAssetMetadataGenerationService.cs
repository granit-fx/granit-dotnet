using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.AssetMetadata.BackgroundJobs;

/// <summary>
/// Per-version orchestration contract injected into the Wolverine handler
/// (<c>DocumentVersionAddedAssetMetadataHandler</c>) so the handler itself
/// stays a thin wrapper. The default implementation runs the full
/// <c>DocumentAssetMetadata</c> lifecycle for one version and bounds
/// parallelism via <c>GranitAssetMetadataOptions.MaxConcurrentExtractions</c>.
/// </summary>
public interface IAssetMetadataGenerationService
{
    /// <summary>
    /// Extracts metadata for one document version. Idempotent — when a
    /// <see cref="Domain.AssetMetadataStatus.Ready"/> row already exists for the
    /// version, the call is a no-op.
    /// </summary>
    Task ExtractAsync(
        Guid documentId,
        Guid? tenantId,
        Guid versionId,
        Guid blobDescriptorId,
        string sourceContentType,
        CancellationToken cancellationToken = default);
}
