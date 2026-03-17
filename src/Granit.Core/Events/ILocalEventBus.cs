namespace Granit.Core.Events;

/// <summary>
/// Publishes events to in-process handlers within the same application boundary.
/// </summary>
/// <remarks>
/// <para>
/// Local events are synchronous, in-process, and not durable. They are suitable for
/// side effects within the same bounded context: cache invalidation, audit trail
/// persistence, denormalization.
/// </para>
/// <para>
/// <b>Default provider:</b> <c>InProcessLocalEventBus</c> (resolves
/// <see cref="ILocalEventHandler{TEvent}"/> from DI, calls sequentially).
/// <b>Wolverine provider:</b> publishes to a Wolverine local queue.
/// </para>
/// <para>
/// <b>When to use local vs distributed:</b>
/// <list type="bullet">
///   <item>Same process, same bounded context → <see cref="ILocalEventBus"/></item>
///   <item>Cross-service, needs at-least-once delivery → <see cref="IDistributedEventBus"/></item>
/// </list>
/// </para>
/// </remarks>
public interface ILocalEventBus
{
    /// <summary>
    /// Publishes an event to all registered <see cref="ILocalEventHandler{TEvent}"/> handlers.
    /// </summary>
    /// <typeparam name="TEvent">The event type (any class).</typeparam>
    /// <param name="localEvent">The event instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
        where TEvent : class;
}
