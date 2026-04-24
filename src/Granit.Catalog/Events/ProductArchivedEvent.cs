using Granit.Catalog.Domain;
using Granit.Events;

namespace Granit.Catalog.Events;

/// <summary>Raised when a <see cref="Product"/> transitions from Published to Archived.</summary>
public sealed record ProductArchivedEvent(Guid ProductId, string Sku) : IDomainEvent;
