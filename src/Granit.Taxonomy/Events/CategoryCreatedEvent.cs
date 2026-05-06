using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a new category is created.</summary>
public sealed record CategoryCreatedEvent(
    Guid CategoryId,
    Guid? TenantId,
    string Scope,
    Guid? ParentId,
    string Name,
    string Path,
    int Depth) : IDomainEvent;
