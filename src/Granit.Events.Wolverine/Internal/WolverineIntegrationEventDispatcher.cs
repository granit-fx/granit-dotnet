using Granit.Events;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IIntegrationEventDispatcher"/>.
/// Publishes each integration event via <see cref="IMessageBus"/> using the non-generic
/// overload so Wolverine routes by runtime type and writes outbox envelopes atomically.
/// </summary>
internal sealed class WolverineIntegrationEventDispatcher(IMessageBus bus) : IIntegrationEventDispatcher
{
    /// <inheritdoc/>
    public async Task DispatchAsync(
        IReadOnlyList<IIntegrationEvent> integrationEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (IIntegrationEvent evt in integrationEvents)
        {
            await bus.PublishAsync(evt).ConfigureAwait(false);
        }
    }
}
