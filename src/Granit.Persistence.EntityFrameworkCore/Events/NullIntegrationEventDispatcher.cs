using Granit.Events;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.Events;

/// <summary>
/// No-op implementation of <see cref="IIntegrationEventDispatcher"/>.
/// </summary>
/// <remarks>
/// Registered by default in <c>Granit.Persistence.EntityFrameworkCore</c> via <c>TryAddSingleton</c>.
/// Replaced by <c>WolverineIntegrationEventDispatcher</c> when
/// <c>Granit.Events.Wolverine</c> is loaded.
/// Logs a warning when events are raised to prevent silent data loss.
/// </remarks>
internal sealed partial class NullIntegrationEventDispatcher(
    ILogger<NullIntegrationEventDispatcher> logger) : IIntegrationEventDispatcher
{
    public Task DispatchAsync(IReadOnlyList<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken = default)
    {
        if (integrationEvents.Count > 0)
        {
            LogEventsDropped(integrationEvents.Count);
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Dropped {Count} integration event(s): no IIntegrationEventDispatcher configured. Add Granit.Events.Wolverine to enable outbox delivery.")]
    private partial void LogEventsDropped(int count);
}
