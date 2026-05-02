using Granit.BlobStorage;
using Granit.Documents.Diagnostics;
using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core / BlobStorage-backed implementation of <see cref="IDocumentService"/>.
/// </summary>
internal sealed class DocumentService(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    IDocumentBootstrapService bootstrap,
    IBlobStorage blobStorage,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILocalEventBus localEventBus,
    DocumentsMetrics metrics) : IDocumentService
{
    /// <summary>
    /// Container name used for every blob created by Granit.Documents. Hosts can layer
    /// per-tenant prefixes via <c>IBlobKeyStrategy</c> in <c>BlobStorage</c>; the
    /// container itself is constant.
    /// </summary>
    internal const string ContainerName = "documents";

    /// <inheritdoc />
    public async Task<PresignedUploadTicket> RequestUploadTicketAsync(
        string fileName,
        string contentType,
        long maxAllowedBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        if (maxAllowedBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAllowedBytes), "Max allowed bytes must be strictly positive.");
        }

        BlobUploadRequest request = new(fileName, contentType, maxAllowedBytes);
        return await blobStorage
            .InitiateUploadAsync(ContainerName, request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Document> FinalizeUploadAsync(
        Guid blobId,
        Guid? folderId,
        Guid ownerUserId,
        string name,
        string? description = null,
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // 1. Confirm the blob with BlobStorage — runs validators (size + magic-bytes), then
        //    transitions Pending → Valid (or Rejected). Idempotent only across the
        //    Pending → Uploading transition; subsequent calls on a Valid blob throw
        //    BlobNotValidException with status Valid, which is the signal we want.
        BlobConfirmationResult confirmation = await blobStorage
            .ConfirmUploadAsync(ContainerName, blobId, cancellationToken)
            .ConfigureAwait(false);
        if (!confirmation.IsValid)
        {
            metrics.RecordQuotaRejected(currentTenant.Id?.ToString());
            throw new InvalidOperationException(
                $"Blob {blobId} did not pass validation: {confirmation.RejectionReason ?? "unknown"}.");
        }

        long sizeBytes = confirmation.SizeBytes
            ?? throw new InvalidOperationException(
                $"Blob {blobId} validated successfully but BlobStorage returned no size.");
        string contentType = confirmation.VerifiedContentType
            ?? throw new InvalidOperationException(
                $"Blob {blobId} validated successfully but BlobStorage returned no content type.");

        // 2. Open a fresh DocumentsDbContext and resolve the target folder, defaulting to
        //    the tenant root via the bootstrap service.
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid effectiveFolderId = folderId ?? await bootstrap
            .EnsureTenantRootAsync(currentTenant.Id, ownerUserId, cancellationToken)
            .ConfigureAwait(false);

        Folder? folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == effectiveFolderId, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            throw new InvalidOperationException(
                $"Target folder {effectiveFolderId} was not found under the current tenant scope.");
        }

        // 3. Create the Document aggregate + initial v1 DocumentVersion atomically.
        var document = Document.Create(
            guidGenerator.Create(), folder, ownerUserId, name, description);

        var version = DocumentVersion.Create(
            guidGenerator.Create(),
            document,
            versionNumber: 1,
            blobDescriptorId: blobId,
            sizeBytes: sizeBytes,
            contentType: contentType,
            contentHash: null,
            uploadedByUserId: ownerUserId,
            uploadedAt: clock.Now,
            commitMessage: commitMessage);

        document.SetCurrentVersion(version.Id);

        context.Documents.Add(document);
        context.DocumentVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 4. Emit the version-added event on the local bus (DocumentVersion is an entity,
        //    not an aggregate root — service-level emission is the canonical pattern).
        await localEventBus.PublishAsync(
            new DocumentVersionAddedEvent(
                document.Id,
                document.TenantId,
                version.Id,
                version.VersionNumber,
                version.BlobDescriptorId,
                version.SizeBytes,
                version.UploadedByUserId),
            cancellationToken).ConfigureAwait(false);

        metrics.RecordUpload(currentTenant.Id?.ToString());
        return document;
    }

    /// <inheritdoc />
    public async Task<PresignedDownloadUrl?> RequestDownloadUrlAsync(
        Guid documentId,
        Guid? versionId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Document? document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }
        if (document.Status != DocumentStatus.Active)
        {
            throw new InvalidOperationException(
                $"Document {documentId} is not active (status: {document.Status}).");
        }

        // Resolve target version: explicit override or the current pointer.
        Guid targetVersionId = versionId ?? document.CurrentVersionId
            ?? throw new InvalidOperationException(
                $"Document {documentId} has no current version yet — finalize an upload first.");

        DocumentVersion? version = await context.DocumentVersions
            .FirstOrDefaultAsync(
                v => v.Id == targetVersionId && v.DocumentId == document.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (version is null)
        {
            return null;
        }

        // Issue the presigned URL via BlobStorage.
        PresignedDownloadUrl url = await blobStorage
            .CreateDownloadUrlAsync(ContainerName, version.BlobDescriptorId, options: null, cancellationToken)
            .ConfigureAwait(false);

        // Audit + metrics. The DocumentDownloadedEvent flows through Granit.Auditing for
        // the ISO 27001 A.12.4.1 trail.
        await localEventBus.PublishAsync(
            new DocumentDownloadedEvent(
                document.Id,
                version.Id,
                document.TenantId,
                requestedByUserId,
                clock.Now,
                url.ExpiresAt),
            cancellationToken).ConfigureAwait(false);

        metrics.RecordDownload(currentTenant.Id?.ToString());
        return url;
    }
}
