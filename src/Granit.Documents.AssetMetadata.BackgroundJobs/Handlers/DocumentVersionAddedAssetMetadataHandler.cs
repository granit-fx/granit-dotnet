using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Events;

namespace Granit.Documents.AssetMetadata.BackgroundJobs.Handlers;

/// <summary>
/// Wolverine handler subscribed to <see cref="DocumentVersionAddedEvent"/>.
/// Delegates the actual extraction to
/// <see cref="IAssetMetadataGenerationService"/> — the handler stays a thin
/// wrapper so the orchestration logic remains testable in isolation.
/// </summary>
public sealed partial class DocumentVersionAddedAssetMetadataHandler
{
    /// <summary>Wolverine-style entry point. Public + static per framework convention.</summary>
    public static Task HandleAsync(
        DocumentVersionAddedEvent evt,
        IAssetMetadataGenerationService generationService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        return generationService.ExtractAsync(
            evt.DocumentId,
            evt.TenantId,
            evt.VersionId,
            evt.BlobDescriptorId,
            evt.ContentType,
            cancellationToken);
    }
}
