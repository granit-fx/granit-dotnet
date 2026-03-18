namespace Granit.Core.Events;

/// <summary>
/// Interface for entities that carry integration events pending dispatch across module boundaries.
/// Implemented by <c>AggregateRoot</c> via <c>AddDistributedEvent()</c>.
/// </summary>
/// <remarks>
/// The <c>DomainEventDispatcherInterceptor</c> scans tracked entities implementing this
/// interface and dispatches collected events in <c>SavingChanges</c> (before the database
/// transaction commits) so that Wolverine can write outbox envelopes atomically.
/// </remarks>
public interface IIntegrationEventSource
{
    /// <summary>Integration events pending dispatch.</summary>
    IReadOnlyCollection<IIntegrationEvent> IntegrationEvents { get; }

    /// <summary>
    /// Clears all pending integration events.
    /// Called by the interceptor after collection.
    /// </summary>
    void ClearIntegrationEvents();
}
