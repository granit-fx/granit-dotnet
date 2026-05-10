using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.Renditions.Domain;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed <see cref="IRenditionService"/>. Composes <see cref="IDocumentService"/>
/// (to honour the tenant filter / trash status of the parent) with
/// <see cref="IRenditionStore"/> and <see cref="IBlobStorage"/>.
/// </summary>
internal sealed class RenditionService(
    IDocumentService documents,
    IRenditionStore renditions,
    IBlobStorage blobStorage) : IRenditionService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentRendition>?> ListAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        Document? doc = await documents.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (doc is null || doc.CurrentVersionId is null)
        {
            return null;
        }

        return await renditions
            .ListForVersionAsync(doc.CurrentVersionId.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PresignedDownloadUrl?> GetDownloadUrlAsync(
        Guid documentId,
        RenditionType type,
        string? format,
        CancellationToken cancellationToken = default)
    {
        Document? doc = await documents.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (doc is null || doc.CurrentVersionId is null)
        {
            return null;
        }

        DocumentRendition? rendition;
        if (!string.IsNullOrEmpty(format))
        {
            rendition = await renditions
                .FindAsync(doc.CurrentVersionId.Value, type, format, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            IReadOnlyList<DocumentRendition> set = await renditions
                .ListForVersionAsync(doc.CurrentVersionId.Value, cancellationToken)
                .ConfigureAwait(false);
            rendition = set.FirstOrDefault(r => r.Type == type && r.Status == RenditionStatus.Ready);
        }

        if (rendition is null || rendition.Status != RenditionStatus.Ready || rendition.BlobDescriptorId is null)
        {
            return null;
        }

        return await blobStorage
            .CreateDownloadUrlAsync(
                DocumentRenditionContainers.Renditions,
                rendition.BlobDescriptorId.Value,
                options: null,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
