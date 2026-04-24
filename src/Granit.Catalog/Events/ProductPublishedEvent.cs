using Granit.Catalog.Domain;
using Granit.Events;

namespace Granit.Catalog.Events;

/// <summary>Raised when a <see cref="Product"/> transitions from Draft to Published.</summary>
public sealed record ProductPublishedEvent(Guid ProductId, string Sku) : IDomainEvent;
