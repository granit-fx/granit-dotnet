using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="IFolderService"/>.
/// </summary>
internal sealed class FolderService(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    IDocumentBootstrapService bootstrap,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock,
    ILocalEventBus localEventBus) : IFolderService
{
    /// <inheritdoc />
    public async Task<Folder> CreateAsync(
        Guid? parentFolderId,
        string name,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? tenantId = currentTenant.Id;
        Guid effectiveParentId = parentFolderId ?? await bootstrap
            .EnsureTenantRootAsync(tenantId, ownerUserId, cancellationToken)
            .ConfigureAwait(false);

        Folder? parent = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == effectiveParentId, cancellationToken)
            .ConfigureAwait(false);
        if (parent is null)
        {
            throw new InvalidOperationException(
                $"Parent folder {effectiveParentId} was not found under the current tenant scope.");
        }

        var folder = Folder.Create(guidGenerator.Create(), parent, name, ownerUserId);
        context.Folders.Add(folder);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return folder;
    }

    /// <inheritdoc />
    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Folder>> ListChildrenAsync(
        Guid? parentFolderId,
        FolderStatus status = FolderStatus.Active,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid effectiveParentId;
        if (parentFolderId.HasValue)
        {
            effectiveParentId = parentFolderId.Value;
        }
        else
        {
            // List children of the tenant root by default. Bootstrap may need to run on
            // first call but is idempotent and cached per request.
            effectiveParentId = await bootstrap
                .EnsureTenantRootAsync(currentTenant.Id, ownerUserId: Guid.Empty, cancellationToken)
                .ConfigureAwait(false);
        }

        return await context.Folders
            .Where(f => f.ParentFolderId == effectiveParentId
                && f.Status == status
                && !f.IsTenantRoot)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Folder>> GetBreadcrumbAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Folder? target = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (target is null || target.IsTenantRoot)
        {
            return [];
        }

        // Path is materialised; ancestors are the folders whose Path is a strict prefix of
        // target.Path. Skip the tenant root (Path = "/") so the breadcrumb starts at the
        // first user-visible folder.
        // Build the list of candidate prefixes from the target path so we can issue one
        // SELECT ... WHERE Path IN (...).
        List<string> prefixes = ExpandAncestorPaths(target.Path);

        List<Folder> ancestors = prefixes.Count == 0
            ? []
            : await context.Folders
                .Where(f => f.TenantId == target.TenantId
                    && !f.IsTenantRoot
                    && prefixes.Contains(f.Path))
                .OrderBy(f => f.Depth)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        ancestors.Add(target);
        return ancestors;
    }

    /// <inheritdoc />
    public async Task<Folder?> RenameAsync(
        Guid id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Folder? folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        folder.Rename(newName);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return folder;
    }

    /// <inheritdoc />
    public async Task<Folder?> MoveAsync(
        Guid id,
        Guid? newParentFolderId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Folder? folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        // Resolve the effective new parent: tenant root when null is passed in.
        Guid effectiveParentId = newParentFolderId ?? await bootstrap
            .EnsureTenantRootAsync(folder.TenantId, folder.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

        Folder? newParent = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == effectiveParentId, cancellationToken)
            .ConfigureAwait(false);
        if (newParent is null)
        {
            throw new InvalidOperationException(
                $"Target parent folder {effectiveParentId} was not found under the current tenant scope.");
        }

        string oldPath = folder.Path;
        int oldDepth = folder.Depth;

        // Aggregate-level validation + own-path update + per-aggregate events.
        folder.MoveTo(newParent);

        string newPath = folder.Path;
        int newDepth = folder.Depth;
        int affectedDescendants = 0;

        // Re-materialise descendants in a single SQL UPDATE. Path is rewritten by replacing
        // the old prefix with the new one; Depth is shifted by the difference between the
        // moved folder's old and new depth — same delta applies to every descendant.
        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            string descendantPrefix = oldPath + Folder.PathSeparator;
            int depthDelta = newDepth - oldDepth;
            int oldPrefixLength = oldPath.Length;

            // CA1845 (Substring vs AsSpan) does not apply to EF Core expression trees:
            // ExecuteUpdateAsync translates Substring() to SQL SUBSTRING; AsSpan has no SQL
            // equivalent.
#pragma warning disable CA1845
            affectedDescendants = await context.Folders
                .Where(f => f.TenantId == folder.TenantId
                    && f.Id != folder.Id
                    && f.Path.StartsWith(descendantPrefix))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(f => f.Path, f => newPath + f.Path.Substring(oldPrefixLength))
                    .SetProperty(f => f.Depth, f => f.Depth + depthDelta),
                    cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore CA1845
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
        {
            // Emit a single tree-level event so downstream consumers (F6.3 ACL cache,
            // F19 search index) can issue prefix-based invalidation rather than N events.
            // Service-emitted (local bus) — per the events convention, aggregate-internal
            // events (FolderMovedEvent, FolderPathChangedEvent on the moved folder itself)
            // are emitted by Folder.MoveTo while this aggregating event is owned by the
            // service that performed the bulk operation.
            await localEventBus.PublishAsync(
                new FolderTreePathChangedEvent(
                    folder.TenantId,
                    folder.Id,
                    oldPath,
                    newPath,
                    affectedDescendants),
                cancellationToken).ConfigureAwait(false);
        }

        return folder;
    }

    /// <inheritdoc />
    public async Task<Folder?> TrashAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Folder? folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return null;
        }

        DateTimeOffset trashedAt = clock.Now;

        // Cascade-trash every active descendant — folders with the requested folder's
        // path as a strict prefix, plus every active document under those folders.
        // Single transaction via SaveChangesAsync atomicity.
        string pathPrefix = folder.Path == Folder.TenantRootPath
            ? Folder.TenantRootPath
            : folder.Path + Folder.PathSeparator;

        List<Folder> descendantFolders = folder.IsTenantRoot
            ? []
            : await context.Folders
                .Where(f => f.TenantId == folder.TenantId
                    && f.Status == FolderStatus.Active
                    && !f.IsTenantRoot
                    && f.Id != folder.Id
                    && f.Path.StartsWith(pathPrefix))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        // Documents whose folder is the target OR any descendant folder. Two queries —
        // direct + descendants — kept separate so each gets a clean SQL shape.
        List<Document> directDocuments = await context.Documents
            .Where(d => d.FolderId == folder.Id && d.Status == DocumentStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Document> descendantDocuments = descendantFolders.Count == 0
            ? []
            : await context.Documents
                .Where(d => descendantFolders.Select(f => f.Id).Contains(d.FolderId)
                    && d.Status == DocumentStatus.Active)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        folder.Trash(trashedAt);
        foreach (Folder descendant in descendantFolders)
        {
            descendant.Trash(trashedAt);
        }
        foreach (Document doc in directDocuments)
        {
            doc.Trash(trashedAt);
        }
        foreach (Document doc in descendantDocuments)
        {
            doc.Trash(trashedAt);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return folder;
    }

    /// <inheritdoc />
    public async Task<Folder?> RestoreAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        Folder? folder = await context.Folders
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null || folder.Status != FolderStatus.Trashed)
        {
            return null;
        }

        // Reject restore when the parent is itself trashed — callers must restore the
        // parent first so the restored folder can become reachable from the tree.
        if (folder.ParentFolderId is { } parentId)
        {
            Folder? parent = await context.Folders
                .FirstOrDefaultAsync(f => f.Id == parentId, cancellationToken)
                .ConfigureAwait(false);
            if (parent is not null && parent.Status == FolderStatus.Trashed)
            {
                throw new InvalidOperationException(
                    $"Cannot restore folder {id}: parent folder {parent.Id} is trashed. Restore the parent first.");
            }
        }

        folder.Restore();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return folder;
    }

    /// <summary>
    /// Expands the path of a folder into the materialised paths of every strict ancestor
    /// (excluding the root <c>"/"</c>). For <c>"/A/B/C"</c> returns <c>["/A", "/A/B"]</c>.
    /// </summary>
    private static List<string> ExpandAncestorPaths(string path)
    {
        List<string> prefixes = [];
        int separatorIndex = path.IndexOf(Folder.PathSeparator, 1, StringComparison.Ordinal);
        while (separatorIndex > 0)
        {
            prefixes.Add(path[..separatorIndex]);
            separatorIndex = path.IndexOf(Folder.PathSeparator, separatorIndex + 1, StringComparison.Ordinal);
        }
        return prefixes;
    }
}
