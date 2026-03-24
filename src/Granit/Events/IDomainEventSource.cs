namespace Granit.Events;

/// <summary>
/// Interface for entities that carry domain events.
/// Implemented by <c>AggregateRoot</c> and its audited variants.
/// Can also be implemented directly by types that do not inherit from
/// <see cref="Domain.Entity"/> (e.g., <c>BlobDescriptor</c>).
/// </summary>
/// <remarks>
/// The <c>DomainEventDispatcherInterceptor</c> scans tracked entities
/// implementing this interface and dispatches collected events after
/// <c>SaveChanges</c> commits.
/// </remarks>
public interface IDomainEventSource
{
    /// <summary>Domain events pending dispatch.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears all pending domain events.
    /// Called by the dispatcher after successful dispatch.
    /// </summary>
    void ClearDomainEvents();
}
