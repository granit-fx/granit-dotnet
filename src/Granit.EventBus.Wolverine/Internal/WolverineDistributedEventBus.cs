using Granit.Core.Events;
using Wolverine;

namespace Granit.EventBus.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IDistributedEventBus"/>.
/// Publishes integration events via <see cref="IMessageBus"/> with outbox support
/// for at-least-once delivery across service boundaries.
/// </summary>
internal sealed class WolverineDistributedEventBus(IMessageBus bus) : IDistributedEventBus
{
    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await bus.PublishAsync(integrationEvent).ConfigureAwait(false);
    }
}
