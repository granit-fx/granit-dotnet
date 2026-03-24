namespace Granit.Events;

/// <summary>
/// Dispatches domain events collected from aggregate roots after persistence.
/// </summary>
/// <remarks>
/// The default implementation uses Wolverine <c>IMessageBus</c> and is registered
/// in <c>Granit.Wolverine</c>. A no-op implementation is provided by default
/// so that modules without Wolverine can still use aggregate roots.
/// </remarks>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a batch of domain events.
    /// Called by the <c>DomainEventDispatcherInterceptor</c> after <c>SaveChanges</c>.
    /// </summary>
    /// <param name="domainEvents">The events to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
