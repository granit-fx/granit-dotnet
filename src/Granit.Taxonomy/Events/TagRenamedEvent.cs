using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a tag is renamed.</summary>
public sealed record TagRenamedEvent(
    Guid TagId,
    string OldName,
    string NewName) : IDomainEvent;
