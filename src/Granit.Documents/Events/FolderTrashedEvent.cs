using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Folder</c> is moved to the trash (soft-delete via domain status).
/// </summary>
public sealed record FolderTrashedEvent(
    Guid FolderId,
    Guid? TenantId,
    DateTimeOffset TrashedAt) : IDomainEvent;
