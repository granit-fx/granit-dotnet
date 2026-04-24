using Granit.Catalog.Domain;
using Granit.Events;

namespace Granit.Catalog.Events;

/// <summary>Raised when a new <see cref="Product"/> is created (in Draft status).</summary>
public sealed record ProductCreatedEvent(
    Guid ProductId,
    string Sku,
    string Name,
    ProductType Type) : IDomainEvent;
