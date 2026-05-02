using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Folder</c> aggregate is created (root or non-root).
/// </summary>
public sealed record FolderCreatedEvent(
    Guid FolderId,
    Guid? TenantId,
    Guid? ParentFolderId,
    string Name,
    string Path,
    bool IsTenantRoot) : IDomainEvent;
