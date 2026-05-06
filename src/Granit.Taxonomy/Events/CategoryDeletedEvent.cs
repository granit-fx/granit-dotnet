using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a category is deleted.</summary>
public sealed record CategoryDeletedEvent(
    Guid CategoryId,
    Guid? TenantId,
    string Scope,
    string Path) : IDomainEvent;
