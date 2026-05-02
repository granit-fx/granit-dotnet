using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised once after a folder is moved (or, in future stories, after a high-level rename
/// affecting many descendants) to notify consumers that every folder whose materialised
/// path started with <see cref="OldPathPrefix"/> now starts with <see cref="NewPathPrefix"/>.
/// </summary>
/// <param name="TenantId">Tenant scope of the affected sub-tree.</param>
/// <param name="MovedFolderId">The folder that was directly moved (the root of the affected sub-tree).</param>
/// <param name="OldPathPrefix">Materialised path of the moved folder before the move (e.g. <c>/A/B</c>).</param>
/// <param name="NewPathPrefix">Materialised path of the moved folder after the move (e.g. <c>/X/B</c>).</param>
/// <param name="AffectedDescendantCount">Number of descendant folders whose path was rewritten in the bulk SQL update.</param>
/// <remarks>
/// <para>
/// Per-folder <see cref="FolderPathChangedEvent"/> events are still emitted for the moved
/// folder itself (by <see cref="Domain.Folder.MoveTo"/>); descendants are repointed by a
/// single SQL <c>UPDATE</c> in the same transaction and announced collectively via this
/// event so consumers (F6.3 ACL cache invalidator, F19 search index) can issue a
/// prefix-based invalidation rather than N per-folder ones.
/// </para>
/// </remarks>
public sealed record FolderTreePathChangedEvent(
    Guid? TenantId,
    Guid MovedFolderId,
    string OldPathPrefix,
    string NewPathPrefix,
    int AffectedDescendantCount) : IDomainEvent;
