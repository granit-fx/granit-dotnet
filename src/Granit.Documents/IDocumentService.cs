using Granit.BlobStorage;
using Granit.Documents.Domain;

namespace Granit.Documents;

/// <summary>
/// Domain orchestration contract for document upload + read operations. The HTTP layer
/// (<c>Granit.Documents.Endpoints</c>) consumes this interface; the EF Core / BlobStorage
/// implementation lives in <c>Granit.Documents.EntityFrameworkCore</c>.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Requests a presigned upload ticket from <c>Granit.BlobStorage</c>. The client uses
    /// the returned <see cref="PresignedUploadTicket.UploadUrl"/> to PUT bytes directly to
    /// the configured cloud provider, then calls <see cref="FinalizeUploadAsync"/> with
    /// the resulting <see cref="PresignedUploadTicket.BlobId"/>.
    /// </summary>
    Task<PresignedUploadTicket> RequestUploadTicketAsync(
        string fileName,
        string contentType,
        long maxAllowedBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the upload with <c>BlobStorage</c> (runs validators, transitions the blob
    /// to <c>Valid</c> if it passes), then atomically creates the <see cref="Document"/>
    /// aggregate and its initial <c>DocumentVersion</c> (<c>VersionNumber = 1</c>).
    /// </summary>
    /// <param name="blobId">Identifier returned by <see cref="RequestUploadTicketAsync"/>.</param>
    /// <param name="folderId">Target folder; <c>null</c> drops the document directly under the tenant root.</param>
    /// <param name="ownerUserId">Identifier of the user who owns the document (typically the caller).</param>
    /// <param name="name">User-facing document name.</param>
    /// <param name="description">Optional free-text description.</param>
    /// <param name="commitMessage">Optional changelog attached to the initial version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created <see cref="Document"/> on success. Throws when the blob is missing,
    /// the folder is missing or trashed, or the blob fails validation.
    /// </returns>
    Task<Document> FinalizeUploadAsync(
        Guid blobId,
        Guid? folderId,
        Guid ownerUserId,
        string name,
        string? description = null,
        string? commitMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a presigned download URL for a document. Defaults to the document's
    /// <see cref="Document.CurrentVersionId"/>; an explicit <paramref name="versionId"/>
    /// fetches a specific historical version (F4.2 list endpoint surfaces them).
    /// </summary>
    /// <param name="documentId">Document to download.</param>
    /// <param name="versionId">Optional specific version. <c>null</c> resolves to <see cref="Document.CurrentVersionId"/>.</param>
    /// <param name="requestedByUserId">User issuing the request (recorded in the <c>DocumentDownloadedEvent</c> for audit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The presigned download URL on success, or <c>null</c> when the document or version
    /// is not found or excluded by the tenant filter. Throws on trashed / permanently-
    /// deleted documents.
    /// </returns>
    Task<PresignedDownloadUrl?> RequestDownloadUrlAsync(
        Guid documentId,
        Guid? versionId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
