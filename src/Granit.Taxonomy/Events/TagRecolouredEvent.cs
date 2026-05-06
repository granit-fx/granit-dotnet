using Granit.Events;

namespace Granit.Taxonomy.Events;

/// <summary>Raised when a tag's colour is changed.</summary>
public sealed record TagRecolouredEvent(
    Guid TagId,
    string OldColor,
    string NewColor) : IDomainEvent;
