using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a tag is deleted.</summary>
public sealed record TagDeletedEvent(
    Guid TagId,
    Guid? TenantId,
    string Scope,
    string Name) : IDomainEvent;
