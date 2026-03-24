using Granit.Events;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="ILocalEventBus"/>.
/// Publishes events to a Wolverine local queue for in-process handler execution
/// with Wolverine's pipeline (validation, context propagation, retry).
/// </summary>
internal sealed class WolverineLocalEventBus(IMessageBus bus) : ILocalEventBus
{
    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(localEvent);
        await bus.PublishAsync(localEvent).ConfigureAwait(false);
    }
}
