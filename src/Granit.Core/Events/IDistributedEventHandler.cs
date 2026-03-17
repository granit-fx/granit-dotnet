// CA1711: the "EventHandler" suffix is intentional — this is a domain event handler contract.
#pragma warning disable CA1711

namespace Granit.Core.Events;

/// <summary>
/// Handles a distributed integration event published via <see cref="IDistributedEventBus"/>.
/// </summary>
/// <typeparam name="TEvent">The integration event type (must implement <see cref="IIntegrationEvent"/>).</typeparam>
/// <remarks>
/// Register implementations in DI as
/// <c>services.AddScoped&lt;IDistributedEventHandler&lt;T&gt;, MyHandler&gt;()</c>.
/// Multiple handlers per event type are supported.
/// </remarks>
public interface IDistributedEventHandler<in TEvent> where TEvent : class, IIntegrationEvent
{
    /// <summary>
    /// Handles the integration event.
    /// </summary>
    /// <param name="integrationEvent">The event instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}
