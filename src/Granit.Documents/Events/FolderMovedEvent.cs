using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Folder</c> is moved to a new parent. A
/// <see cref="FolderPathChangedEvent"/> is emitted alongside since the
/// materialised <c>Path</c> changes too.
/// </summary>
public sealed record FolderMovedEvent(
    Guid FolderId,
    Guid? OldParentFolderId,
    Guid NewParentFolderId) : IDomainEvent;
