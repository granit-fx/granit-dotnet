// CA1711: the "EventHandler" suffix is intentional — this is a domain event handler contract.
#pragma warning disable CA1711

namespace Granit.Core.Events;

/// <summary>
/// Handles a local event published via <see cref="ILocalEventBus"/>.
/// </summary>
/// <typeparam name="TEvent">The event type to handle.</typeparam>
/// <remarks>
/// Register implementations in DI as <c>services.AddScoped&lt;ILocalEventHandler&lt;T&gt;, MyHandler&gt;()</c>.
/// Multiple handlers per event type are supported — all are called sequentially.
/// </remarks>
public interface ILocalEventHandler<in TEvent> where TEvent : class
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="localEvent">The event instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent localEvent, CancellationToken cancellationToken = default);
}
