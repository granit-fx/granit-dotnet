using Granit.Documents.Domain;

namespace Granit.Documents;

/// <summary>
/// Domain orchestration contract for folder CRUD operations. The HTTP layer
/// (<c>Granit.Documents.Endpoints</c>) consumes this interface; the EF Core
/// implementation lives in <c>Granit.Documents.EntityFrameworkCore</c>.
/// </summary>
/// <remarks>
/// All operations are tenant-scoped through the ambient <c>ICurrentTenant</c> when
/// the multi-tenant filter is active; <see cref="CreateAsync"/> and breadcrumb
/// resolution implicitly target the tenant root via <see cref="IDocumentBootstrapService"/>
/// when no explicit parent identifier is supplied.
/// </remarks>
public interface IFolderService
{
    /// <summary>
    /// Creates a new folder under <paramref name="parentFolderId"/>; when <c>null</c>
    /// the folder is created directly under the tenant root.
    /// </summary>
    /// <returns>The created <see cref="Folder"/> aggregate.</returns>
    Task<Folder> CreateAsync(
        Guid? parentFolderId,
        string name,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the folder with the given identifier, or <c>null</c> when not found
    /// or when the caller's tenant filter excludes it.
    /// </summary>
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active folders under <paramref name="parentFolderId"/>; when <c>null</c>
    /// the result is the children of the tenant root. The tenant root itself is
    /// always excluded from the result.
    /// </summary>
    Task<IReadOnlyList<Folder>> ListChildrenAsync(
        Guid? parentFolderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the chain of folders from the tenant root (excluded) down to the
    /// folder with the given identifier (included). The order is parent-first
    /// (closest-to-root → leaf). Returns an empty list when the folder is the
    /// tenant root or when the folder is not found.
    /// </summary>
    Task<IReadOnlyList<Folder>> GetBreadcrumbAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the folder with the given identifier. Returns the updated folder, or
    /// <c>null</c> when the folder is not found or excluded by the tenant filter.
    /// </summary>
    Task<Folder?> RenameAsync(Guid id, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the folder identified by <paramref name="id"/> under the parent identified
    /// by <paramref name="newParentFolderId"/> (or the tenant root when <c>null</c>) and
    /// re-materialises the <c>Path</c> / <c>Depth</c> of every descendant folder in a
    /// single SQL <c>UPDATE</c> within the same transaction.
    /// </summary>
    /// <returns>The updated moved folder, or <c>null</c> when the folder is not found.</returns>
    /// <remarks>
    /// <para>
    /// Validation is performed by the <see cref="Folder.MoveTo"/> aggregate method
    /// (cycle / descendant / cross-tenant / trashed-target rejection); invalid moves
    /// surface as <see cref="InvalidOperationException"/> from the call site.
    /// </para>
    /// <para>
    /// On success the moved folder emits a per-aggregate <see cref="Events.FolderMovedEvent"/>
    /// + <see cref="Events.FolderPathChangedEvent"/>, and the service emits a single
    /// <see cref="Events.FolderTreePathChangedEvent"/> aggregating the prefix change
    /// for downstream consumers (cache invalidation, search index).
    /// </para>
    /// </remarks>
    Task<Folder?> MoveAsync(
        Guid id,
        Guid? newParentFolderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trashes the folder with the given identifier. Returns the trashed folder, or
    /// <c>null</c> when the folder is not found or excluded by the tenant filter.
    /// </summary>
    /// <remarks>
    /// Cascade trashing of descendants is performed by the service implementation in a
    /// single transaction — every active descendant folder is set to
    /// <see cref="FolderStatus.Trashed"/> with the same <c>TrashedAt</c>.
    /// </remarks>
    Task<Folder?> TrashAsync(Guid id, CancellationToken cancellationToken = default);
}
