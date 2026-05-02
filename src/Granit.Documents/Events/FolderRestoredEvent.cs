using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a trashed <c>Folder</c> is restored to active status.
/// </summary>
public sealed record FolderRestoredEvent(
    Guid FolderId,
    Guid? TenantId) : IDomainEvent;
