using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Document</c> is moved to a different folder.
/// </summary>
public sealed record DocumentMovedEvent(
    Guid DocumentId,
    Guid OldFolderId,
    Guid NewFolderId) : IDomainEvent;
