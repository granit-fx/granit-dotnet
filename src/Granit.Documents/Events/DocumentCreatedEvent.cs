using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Document</c> aggregate is created.
/// </summary>
public sealed record DocumentCreatedEvent(
    Guid DocumentId,
    Guid? TenantId,
    Guid FolderId,
    Guid OwnerUserId,
    string Name) : IDomainEvent;
