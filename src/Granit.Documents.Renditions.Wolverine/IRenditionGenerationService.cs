using System.Threading;
using System.Threading.Tasks;

namespace Granit.Documents.Renditions.Wolverine;

/// <summary>
/// Per-rendition orchestration contract injected into the Wolverine handler
/// (<c>DocumentVersionAddedRenditionsHandler</c>) so the handler itself stays a thin
/// wrapper. The default implementation runs the full
/// <see cref="DocumentRendition"/> lifecycle for one target and bounds parallelism
/// via <c>GranitRenditionsOptions.MaxConcurrentGenerations</c>.
/// </summary>
public interface IRenditionGenerationService
{
    /// <summary>Generates the rendition declared by <paramref name="target"/>.</summary>
    Task GenerateAsync(
        Guid documentId,
        Guid? tenantId,
        Guid versionId,
        Guid blobDescriptorId,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default);
}
