using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Document</c>'s <c>CurrentVersionId</c> changes — either because a
/// new <c>DocumentVersion</c> has been finalised (F4.1) or because an older version
/// has been restored as current (F4.3).
/// </summary>
public sealed record DocumentCurrentVersionChangedEvent(
    Guid DocumentId,
    Guid? OldCurrentVersionId,
    Guid NewCurrentVersionId) : IDomainEvent;
