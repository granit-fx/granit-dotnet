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
    public async Task<DocumentVersion?> AppendVersionAsync(
        Guid documentId,
        Guid blobId,
        Guid uploadedByUserId,
        string? commitMessage = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Confirm the blob — runs validators and transitions Pending → Valid. Done
        //    outside the retry loop because the transition is non-idempotent past Valid:
        //    re-calling ConfirmUploadAsync on a Valid blob throws BlobNotValidException.
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

        // 2. Append loop — at most 2 attempts. The unique index on
        //    (DocumentId, VersionNumber) plus the optimistic-concurrency token on
        //    Document.RowVersion together guarantee that two parallel callers cannot
        //    both succeed with the same VersionNumber: the second writer either trips
        //    the unique constraint (DbUpdateException) or the row-version check
        //    (DbUpdateConcurrencyException). Either way we re-read and retry once;
        //    a second collision propagates as the original exception.
        const int MaxAttempts = 2;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await TryAppendVersionAsync(
                    documentId, blobId, uploadedByUserId, sizeBytes, contentType, commitMessage,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // RowVersion drifted under us — reload and try again.
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Unique-index collision on (DocumentId, VersionNumber) — reload and retry.
            }
        }

        // Unreachable: the loop either returns inside the try, or the final attempt's
        // exception propagates without being caught (the `when` filters guard only
        // the non-final attempts).
        throw new InvalidOperationException(
            $"AppendVersionAsync exited the retry loop unexpectedly for document {documentId}.");
    }

    private async Task<DocumentVersion?> TryAppendVersionAsync(
        Guid documentId,
        Guid blobId,
        Guid uploadedByUserId,
        long sizeBytes,
        string contentType,
        string? commitMessage,
        CancellationToken cancellationToken)
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

        // Domain enforces "active document" via SetCurrentVersion → ThrowIfNotActive.

        int nextVersionNumber = (await context.DocumentVersions
            .Where(v => v.DocumentId == document.Id)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken)
            .ConfigureAwait(false) ?? 0) + 1;

        var version = DocumentVersion.Create(
            guidGenerator.Create(),
            document,
            versionNumber: nextVersionNumber,
            blobDescriptorId: blobId,
            sizeBytes: sizeBytes,
            contentType: contentType,
            contentHash: null,
            uploadedByUserId: uploadedByUserId,
            uploadedAt: clock.Now,
            commitMessage: commitMessage);

        // SetCurrentVersion bumps Document.RowVersion — feeds the optimistic concurrency
        // check that detects parallel appenders racing on the same document.
        document.SetCurrentVersion(version.Id);

        context.DocumentVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
        return version;
    }

    /// <inheritdoc />
    public async Task<DocumentVersionPage?> ListVersionsAsync(
        Guid documentId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "skip must be non-negative.");
        }
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "take must be strictly positive.");
        }

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Pull only the columns we need from Document — the version listing endpoint does
        // not need the full aggregate, just the existence + current-version pointer.
        var doc = await context.Documents
            .Where(d => d.Id == documentId)
            .Select(d => new { d.Id, d.CurrentVersionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        IQueryable<DocumentVersion> versionsQuery = context.DocumentVersions
            .Where(v => v.DocumentId == documentId);

        int totalCount = await versionsQuery
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DocumentVersion> page = await versionsQuery
            .OrderByDescending(v => v.VersionNumber)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DocumentVersionPage(page, totalCount, doc.CurrentVersionId);
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

    /// <inheritdoc />
    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Document?> RenameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Document? document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        document.Rename(newName);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }

    /// <inheritdoc />
    public async Task<Document?> UpdateDescriptionAsync(
        Guid id,
        string? newDescription,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Document? document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        document.UpdateDescription(newDescription);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }

    /// <inheritdoc />
    public async Task<Document?> MoveAsync(
        Guid id,
        Guid? newFolderId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Document? document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        Guid effectiveFolderId = newFolderId ?? await bootstrap
            .EnsureTenantRootAsync(document.TenantId, document.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

        Folder? newFolder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == effectiveFolderId, cancellationToken)
            .ConfigureAwait(false);
        if (newFolder is null)
        {
            throw new InvalidOperationException(
                $"Target folder {effectiveFolderId} was not found under the current tenant scope.");
        }

        document.MoveTo(newFolder);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }

    /// <inheritdoc />
    public async Task<Document?> TrashAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Document? document = await context.Documents
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        document.Trash(clock.Now);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }
}
