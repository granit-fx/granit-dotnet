namespace Granit.Core.Events;

/// <summary>
/// Publishes integration events durably across service boundaries.
/// </summary>
/// <remarks>
/// <para>
/// Distributed events provide at-least-once delivery guarantees via the configured
/// provider (Wolverine outbox, RabbitMQ, Azure Service Bus, etc.). Events must
/// implement <see cref="IIntegrationEvent"/> to enforce serializable DTOs.
/// </para>
/// <para>
/// <b>Default provider:</b> <c>InProcessDistributedEventBus</c> (dev/test only, not durable).
/// <b>Wolverine provider:</b> publishes via <c>IMessageBus</c> with outbox.
/// </para>
/// <para>
/// <b>When to use distributed:</b>
/// <list type="bullet">
///   <item>Event consumers may be in a different service / process</item>
///   <item>At-least-once delivery is required</item>
///   <item>Event must survive process crashes (outbox)</item>
/// </list>
/// </para>
/// </remarks>
public interface IDistributedEventBus
{
    /// <summary>
    /// Publishes an integration event to all registered
    /// <see cref="IDistributedEventHandler{TEvent}"/> handlers.
    /// </summary>
    /// <typeparam name="TEvent">The integration event type (must implement <see cref="IIntegrationEvent"/>).</typeparam>
    /// <param name="integrationEvent">The event instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
