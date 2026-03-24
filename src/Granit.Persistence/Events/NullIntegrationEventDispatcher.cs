using Granit.Events;

namespace Granit.Persistence.Events;

/// <summary>
/// No-op implementation of <see cref="IIntegrationEventDispatcher"/>.
/// </summary>
/// <remarks>
/// Registered by default in <c>Granit.Persistence</c> via <c>TryAddSingleton</c>.
/// Replaced by <c>WolverineIntegrationEventDispatcher</c> when
/// <c>Granit.Events.Wolverine</c> is loaded.
/// </remarks>
internal sealed class NullIntegrationEventDispatcher : IIntegrationEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
