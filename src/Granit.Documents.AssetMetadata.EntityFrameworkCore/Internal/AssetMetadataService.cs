using System;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Documents.Domain;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed <see cref="IAssetMetadataService"/>. Composes
/// <see cref="IDocumentService"/> (to honour the parent tenant filter / trash
/// status) with <see cref="IAssetMetadataStore"/>.
/// </summary>
internal sealed class AssetMetadataService(
    IDocumentService documents,
    IAssetMetadataStore store) : IAssetMetadataService
{
    /// <inheritdoc />
    public async Task<DocumentAssetMetadata?> GetForCurrentVersionAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        Document? doc = await documents.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (doc is null || doc.CurrentVersionId is null)
        {
            return null;
        }

        return await store.GetByVersionAsync(doc.CurrentVersionId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DocumentAssetMetadata?> GetForVersionAsync(
        Guid documentId, Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        Document? doc = await documents.GetByIdAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        DocumentAssetMetadata? row = await store
            .GetByVersionAsync(documentVersionId, cancellationToken)
            .ConfigureAwait(false);

        return row is null || row.DocumentId != documentId ? null : row;
    }
}
