using Granit.Documents.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="IDocumentShareService"/>.
/// </summary>
internal sealed class DocumentShareService(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock) : IDocumentShareService
{
    /// <inheritdoc />
    public async Task<DocumentShare?> GrantOnFolderAsync(
        Guid folderId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        bool isDefault,
        Guid createdByUserId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Folder? folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        var share = DocumentShare.ShareToFolder(
            id: guidGenerator.Create(),
            tenantId: folder.TenantId ?? currentTenant.Id,
            folderId: folder.Id,
            granteeType: granteeType,
            granteeId: granteeId,
            permission: permission,
            isDefault: isDefault,
            createdByUserId: createdByUserId,
            createdAt: clock.Now,
            expiresAt: expiresAt);

        context.DocumentShares.Add(share);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return share;
    }

    /// <inheritdoc />
    public async Task<DocumentShare?> GrantOnDocumentAsync(
        Guid documentId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        Guid createdByUserId,
        DateTimeOffset? expiresAt = null,
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

        var share = DocumentShare.ShareToDocument(
            id: guidGenerator.Create(),
            tenantId: document.TenantId ?? currentTenant.Id,
            documentId: document.Id,
            granteeType: granteeType,
            granteeId: granteeId,
            permission: permission,
            createdByUserId: createdByUserId,
            createdAt: clock.Now,
            expiresAt: expiresAt);

        context.DocumentShares.Add(share);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return share;
    }

    /// <inheritdoc />
    public async Task<bool> RevokeAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DocumentShare? share = await context.DocumentShares
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken)
            .ConfigureAwait(false);
        if (share is null)
        {
            return false;
        }

        // Emit the revoked event before delete so event dispatch picks it up alongside
        // SaveChanges. The aggregate is removed in the same transaction.
        share.Revoke();
        context.DocumentShares.Remove(share);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentShare>> ListForFolderAsync(
        Guid folderId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        return await context.DocumentShares
            .Where(s => s.TargetType == ShareTargetType.Folder
                && s.FolderId == folderId
                && (s.ExpiresAt == null || s.ExpiresAt > now))
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentShare>> ListForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        return await context.DocumentShares
            .Where(s => s.TargetType == ShareTargetType.Document
                && s.DocumentId == documentId
                && (s.ExpiresAt == null || s.ExpiresAt > now))
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
