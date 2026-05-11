using Granit.BlobStorage;
using Granit.BlobStorage.Options;
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
    /// Appends a new <c>DocumentVersion</c> to an existing document. Confirms the blob
    /// with <c>BlobStorage</c>, computes <c>VersionNumber = max + 1</c>, persists the new
    /// row, and updates <see cref="Document.CurrentVersionId"/> via the optimistic-
    /// concurrency-protected <see cref="Document.SetCurrentVersion"/> behavior. On
    /// concurrent appends, the implementation retries once after re-reading the latest
    /// state; after that, the concurrent <c>DbUpdateConcurrencyException</c> surfaces.
    /// </summary>
    /// <param name="documentId">Document to append a version to.</param>
    /// <param name="blobId">Identifier returned by <see cref="RequestUploadTicketAsync"/>.</param>
    /// <param name="uploadedByUserId">Identifier of the user issuing the new version.</param>
    /// <param name="commitMessage">Optional changelog attached to the new version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The newly-created <see cref="DocumentVersion"/> on success, or <c>null</c> when the
    /// document is not found or excluded by the tenant filter. Throws
    /// <see cref="InvalidOperationException"/> when the document is trashed or
    /// permanently-deleted, or when the blob fails validation.
    /// </returns>
    Task<DocumentVersion?> AppendVersionAsync(
        Guid documentId,
        Guid blobId,
        Guid uploadedByUserId,
        string? commitMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paged slice of the document's version history, ordered by
    /// <see cref="DocumentVersion.VersionNumber"/> descending (latest first), along with
    /// the parent document's <see cref="Document.CurrentVersionId"/> so the HTTP mapper
    /// can flag which row is the active version.
    /// </summary>
    /// <param name="documentId">Document whose history is requested.</param>
    /// <param name="skip">Number of versions to skip (for pagination); must be ≥ 0.</param>
    /// <param name="take">Maximum number of versions to return; must be &gt; 0.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The page on success, or <c>null</c> when the document is not found or excluded by
    /// the tenant filter. An empty <c>Versions</c> list with <c>TotalCount = 0</c> is
    /// possible only for a document that has not yet had its first version finalised.
    /// </returns>
    Task<DocumentVersionPage?> ListVersionsAsync(
        Guid documentId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a presigned download URL for a document. Defaults to the document's
    /// <see cref="Document.CurrentVersionId"/>; an explicit <paramref name="versionId"/>
    /// fetches a specific historical version surfaced by <see cref="ListVersionsAsync"/>.
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

    /// <summary>
    /// Returns the document with the given identifier, or <c>null</c> when not found
    /// or excluded by the tenant filter.
    /// </summary>
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the document. Returns the updated aggregate, or <c>null</c> when the
    /// document is not found or excluded by the tenant filter.
    /// </summary>
    /// <remarks>Aggregate-level invariants surface as <see cref="InvalidOperationException"/>.</remarks>
    Task<Document?> RenameAsync(Guid id, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the optional description (<c>null</c> clears it). Returns the updated
    /// aggregate, or <c>null</c> when the document is not found.
    /// </summary>
    Task<Document?> UpdateDescriptionAsync(Guid id, string? newDescription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the document under <paramref name="newFolderId"/> (or the tenant root when
    /// <c>null</c>). Returns the updated aggregate, or <c>null</c> when the document is
    /// not found.
    /// </summary>
    /// <remarks>
    /// Cross-tenant moves and moves into a trashed folder surface as
    /// <see cref="InvalidOperationException"/>; missing target folder surfaces the same way.
    /// </remarks>
    Task<Document?> MoveAsync(Guid id, Guid? newFolderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the document to the trash. Returns the trashed aggregate, or <c>null</c>
    /// when the document is not found.
    /// </summary>
    Task<Document?> TrashAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a trashed document. Returns the restored aggregate, or <c>null</c> when
    /// the document is not found / not currently trashed / excluded by the tenant
    /// filter. Throws <see cref="InvalidOperationException"/> when the document's
    /// folder is itself trashed (callers must restore the folder first).
    /// </summary>
    Task<Document?> RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a trashed document (F8.2). Soft-deletes every
    /// <c>DocumentVersion</c>'s <c>BlobDescriptor</c> through
    /// <see cref="IBlobStorage.DeleteAsync"/> (the bytes are removed; the audit row
    /// stays for the configured retention), decrements the tenant's storage quota, and
    /// promotes the document to <c>Status = PermanentlyDeleted</c>. Returns the deleted
    /// aggregate, or <c>null</c> when not found / not trashed.
    /// </summary>
    Task<Document?> PermanentlyDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paged slice of the tenant's trashed documents (F8.2), ordered by
    /// <c>TrashedAt</c> descending so the most recently trashed appear first.
    /// </summary>
    Task<TrashedDocumentPage> ListTrashedAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <see cref="DocumentVersion"/> with the given identifier, or <c>null</c>
    /// when not found / excluded by the tenant filter. Convenience accessor consumed by
    /// downstream post-upload flows (F17.4 / F17.9) that need to re-read a version's
    /// current <c>BlobDescriptorId</c> rather than rely on a stale event snapshot.
    /// </summary>
    Task<DocumentVersion?> GetVersionByIdAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically swaps the blob backing a <see cref="DocumentVersion"/> for a freshly-
    /// uploaded sanitised one. Reserved for GDPR-driven scrub flows (F17.9 — GPS strip on
    /// upload).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The implementation updates the version's <c>BlobDescriptorId</c> + <c>SizeBytes</c>,
    /// soft-deletes the old blob via <see cref="IBlobStorage.DeleteAsync"/>, rebalances the
    /// tenant's storage quota (decrement old, increment new — the scrubbed blob is
    /// typically smaller) and publishes a <c>DocumentBlobScrubbedEvent</c> for the audit
    /// trail. All inside a single <c>SaveChangesAsync</c>.
    /// </para>
    /// <para>
    /// Returns the updated <see cref="DocumentVersion"/>, or <c>null</c> when the version
    /// is not found / excluded by the tenant filter.
    /// </para>
    /// </remarks>
    /// <param name="versionId">Identifier of the version whose blob to replace.</param>
    /// <param name="newBlobDescriptorId">Identifier of the freshly-uploaded scrubbed blob (already <c>Valid</c>).</param>
    /// <param name="newSizeBytes">Verified size of the scrubbed blob.</param>
    /// <param name="reason">Audit reason recorded on <c>DocumentBlobScrubbedEvent</c> (e.g. <c>"gps-strip"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DocumentVersion?> ReplaceVersionBlobAsync(
        Guid versionId,
        Guid newBlobDescriptorId,
        long newSizeBytes,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a presigned download URL for the document's current version, bypassing
    /// the multi-tenant query filter — reserved for anonymous public-link redemption
    /// (F18.3). The caller is expected to have already authorised the request through
    /// a non-HTTP mechanism (bearer-token hash lookup on a <c>DocumentPublicLink</c>).
    /// </summary>
    /// <remarks>
    /// Returns <c>null</c> when the document is not found, has no current version yet,
    /// or is trashed / permanently-deleted. No <c>DocumentDownloadedEvent</c> is raised
    /// — public-link consumption emits its own <c>DocumentPublicLinkConsumedEvent</c>
    /// for the ISO 27001 audit trail.
    /// </remarks>
    /// <param name="documentId">Document whose current version should be served.</param>
    /// <param name="options">
    /// Optional download parameters — set <see cref="DownloadUrlOptions.DownloadFileName"/>
    /// to force <c>Content-Disposition: attachment</c>; leave <c>null</c> for inline preview.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PresignedDownloadUrl?> CreatePublicDownloadUrlAsync(
        Guid documentId,
        DownloadUrlOptions? options,
        CancellationToken cancellationToken = default);
}
