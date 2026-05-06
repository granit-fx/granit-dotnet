using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a tag's <c>HideOnEntityCard</c> flag is toggled.</summary>
public sealed record TagHideOnEntityCardChangedEvent(
    Guid TagId,
    bool HideOnEntityCard) : IDomainEvent;
