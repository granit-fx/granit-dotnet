using Granit.Events;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IDomainEventDispatcher"/>.
/// Publishes each domain event via <see cref="IMessageBus"/> using the non-generic
/// overload so Wolverine routes by runtime type to locally discovered handlers.
/// </summary>
internal sealed class WolverineDomainEventDispatcher(IMessageBus bus) : IDomainEventDispatcher
{
    /// <inheritdoc/>
    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (IDomainEvent evt in domainEvents)
        {
            await bus.PublishAsync(evt).ConfigureAwait(false);
        }
    }
}
