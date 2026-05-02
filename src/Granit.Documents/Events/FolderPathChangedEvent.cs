using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised whenever a <c>Folder</c>'s materialised <c>Path</c> changes — on rename
/// or move. Consumers (F6.3 ACL cache invalidator, search index) use this event to
/// invalidate or refresh derived state for the folder and all descendants.
/// </summary>
public sealed record FolderPathChangedEvent(
    Guid FolderId,
    Guid? TenantId,
    string OldPath,
    string NewPath) : IDomainEvent;
