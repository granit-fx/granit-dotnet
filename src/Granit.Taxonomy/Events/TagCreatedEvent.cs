using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a new tag is created.</summary>
public sealed record TagCreatedEvent(
    Guid TagId,
    Guid? TenantId,
    string Scope,
    string Name,
    string Color,
    bool HideOnEntityCard) : IDomainEvent;
