namespace Granit.Events;

/// <summary>
/// Dispatches integration events collected from aggregate roots before persistence commits.
/// </summary>
/// <remarks>
/// The default implementation is a no-op registered in <c>Granit.Persistence.EntityFrameworkCore</c>.
/// The Wolverine implementation (<c>WolverineIntegrationEventDispatcher</c>) routes events
/// to <c>IMessageBus</c> so that Wolverine persists outbox envelopes atomically within the
/// current EF Core transaction.
/// </remarks>
public interface IIntegrationEventDispatcher
{
    /// <summary>
    /// Dispatches a batch of integration events.
    /// Called by the <c>DomainEventDispatcherInterceptor</c> and
    /// <c>EntityLifecycleEventInterceptor</c> during <c>SavingChanges</c>.
    /// </summary>
    /// <param name="integrationEvents">The events to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchAsync(IReadOnlyList<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken = default);
}
