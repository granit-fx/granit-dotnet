using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a category is renamed.</summary>
public sealed record CategoryRenamedEvent(
    Guid CategoryId,
    string OldName,
    string NewName) : IDomainEvent;
