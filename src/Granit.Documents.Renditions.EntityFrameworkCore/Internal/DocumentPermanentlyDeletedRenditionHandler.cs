using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents;
using Granit.Documents.Events;
using Granit.Documents.Renditions;
using Granit.Documents.Renditions.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Internal;

/// <summary>
/// Subscribes to <see cref="DocumentPermanentlyDeletedEvent"/> and cascades on the
/// rendition side: every <see cref="DocumentRendition"/> attached to the deleted
/// document has its underlying blob soft-deleted through <c>IBlobStorage.DeleteAsync</c>,
/// the rendition rows are removed, and the tenant's <c>RenditionUsageBytes</c> counter
/// is decremented by the released total.
/// </summary>
/// <remarks>
/// Lives in the renditions EFC package so the parent <c>Granit.Documents</c> module
/// stays unaware of the renditions surface (clean dependency direction).
/// </remarks>
public sealed partial class DocumentPermanentlyDeletedRenditionHandler
{
    /// <summary>Wolverine-style handler entry point. Public + static per framework convention.</summary>
    public static async Task HandleAsync(
        DocumentPermanentlyDeletedEvent evt,
        IRenditionStore renditionStore,
        IBlobStorage blobStorage,
        ITenantQuotaService quotas,
        ILogger<DocumentPermanentlyDeletedRenditionHandler> logger,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentRendition> renditions = await renditionStore
            .ListForDocumentAsync(evt.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (renditions.Count == 0)
        {
            return;
        }

        long releasedBytes = 0L;
        foreach (DocumentRendition r in renditions)
        {
            if (r.BlobDescriptorId is { } blobId)
            {
                await blobStorage.DeleteAsync(
                    DocumentRenditionContainers.Renditions,
                    blobId,
                    deletionReason: $"Granit.Documents.Renditions cascade on permanent-delete of document {evt.DocumentId}",
                    cancellationToken).ConfigureAwait(false);
            }
            releasedBytes += r.SizeBytes ?? 0L;
        }

        await renditionStore
            .DeleteForDocumentAsync(evt.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (releasedBytes > 0 && evt.TenantId is { } tenantId)
        {
            await quotas.DecrementRenditionAsync(tenantId, releasedBytes, cancellationToken)
                .ConfigureAwait(false);
        }

        LogCascade(logger, renditions.Count, releasedBytes, evt.DocumentId);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Granit.Documents.Renditions cascaded permanent-delete: dropped {Count} rendition(s) (~{ReleasedBytes} bytes) for document {DocumentId}.")]
    private static partial void LogCascade(ILogger logger, int count, long releasedBytes, Guid documentId);
}

