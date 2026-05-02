using Granit.Events;

namespace Granit.Documents.Events;

/// <summary>
/// Raised when a <c>Document</c> is renamed.
/// </summary>
public sealed record DocumentRenamedEvent(
    Guid DocumentId,
    string OldName,
    string NewName) : IDomainEvent;
