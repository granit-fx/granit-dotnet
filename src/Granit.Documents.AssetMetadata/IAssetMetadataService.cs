using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Domain;

namespace Granit.Documents.AssetMetadata;

/// <summary>
/// Orchestration contract for the asset-metadata HTTP surface (F17.3). Sits
/// between the HTTP layer (<c>Granit.Documents.AssetMetadata.Endpoints</c>)
/// and the storage primitive — consumers inject this interface rather than
/// wiring <see cref="IAssetMetadataStore"/> + <c>IDocumentService</c> by hand.
/// </summary>
public interface IAssetMetadataService
{
    /// <summary>
    /// Returns the metadata row attached to the document's <i>current</i>
    /// version, or <c>null</c> when the document is missing / excluded by the
    /// tenant filter, or when extraction has not yet produced a row.
    /// </summary>
    Task<DocumentAssetMetadata?> GetForCurrentVersionAsync(
        Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the metadata row attached to a <i>specific</i> version of the
    /// document, or <c>null</c> when the document is missing / excluded by the
    /// tenant filter or no metadata row exists for that version. The parent
    /// document is resolved through <c>IDocumentService</c> so tenant filtering
    /// and trash status are honoured.
    /// </summary>
    Task<DocumentAssetMetadata?> GetForVersionAsync(
        Guid documentId, Guid documentVersionId, CancellationToken cancellationToken = default);
}
