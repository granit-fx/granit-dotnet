using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;

/// <summary>
/// Subscribes to <see cref="DocumentPermanentlyDeletedEvent"/> and drops every
/// <c>DocumentAssetMetadata</c> row attached to the deleted document. The
/// asset-metadata rows hold no extra blobs (the source blob is owned by the
/// parent <c>DocumentVersion</c>), so cascade is a pure row delete.
/// </summary>
/// <remarks>
/// Lives in the asset-metadata EFC package so the parent <c>Granit.Documents</c>
/// module stays unaware of asset-metadata (clean dependency direction; mirrors
/// the rendition handler).
/// </remarks>
public class DocumentPermanentlyDeletedAssetMetadataHandler
{
    /// <summary>Wolverine-style handler entry point. Public + static per framework convention.</summary>
    public static async Task HandleAsync(
        DocumentPermanentlyDeletedEvent evt,
        IAssetMetadataStore store,
        ILogger<DocumentPermanentlyDeletedAssetMetadataHandler> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evt);

        await store.DeleteForDocumentAsync(evt.DocumentId, cancellationToken).ConfigureAwait(false);
        LogCascade(logger, evt.DocumentId);
    }

    private static readonly Action<ILogger, Guid, Exception?> LogCascadeMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(DocumentPermanentlyDeletedAssetMetadataHandler)),
            "Granit.Documents.AssetMetadata cascaded permanent-delete: dropped rows for document {DocumentId}.");

    private static void LogCascade(ILogger logger, Guid documentId) =>
        LogCascadeMessage(logger, documentId, null);
}
