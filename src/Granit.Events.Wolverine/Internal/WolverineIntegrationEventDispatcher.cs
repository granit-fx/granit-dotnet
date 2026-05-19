using Granit.Events.Diagnostics;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IIntegrationEventDispatcher"/>.
/// Publishes each integration event via <see cref="IMessageBus"/> using the non-generic
/// overload so Wolverine routes by runtime type and writes outbox envelopes atomically.
/// </summary>
/// <remarks>
/// When the host has not yet started (data seeding, <c>--migrate</c> mode),
/// integration events are silently skipped. Seeding creates initial state —
/// there are no downstream consumers that need to react to it. The skip happens
/// before any interaction with <see cref="IMessageBus"/>, preventing Wolverine
/// from attempting outbox envelope writes.
/// </remarks>
internal sealed partial class WolverineIntegrationEventDispatcher(
    IMessageBus bus,
    WolverineHostReadiness readiness,
    EventsMetrics metrics,
    ILogger<WolverineIntegrationEventDispatcher> logger) : IIntegrationEventDispatcher
{
    private int _skipWarned;

    /// <inheritdoc/>
    public async Task DispatchAsync(
        IReadOnlyList<IIntegrationEvent> integrationEvents,
        CancellationToken cancellationToken = default)
    {
        if (readiness.IsReady)
        {
            foreach (IIntegrationEvent evt in integrationEvents)
            {
                await bus.PublishAsync(evt).ConfigureAwait(false);
            }

            return;
        }

        // Wolverine not started — skip integration events (no consumers during seeding/migrate).
        // Skip before IMessageBus interaction to prevent outbox envelope writes.
        foreach (IIntegrationEvent evt in integrationEvents)
        {
            metrics.RecordEventPublished(null, "integration-skipped", evt.GetType().Name);
        }

        if (integrationEvents.Count > 0
            && Interlocked.CompareExchange(ref _skipWarned, 1, 0) == 0)
        {
            LogSkippingIntegrationEvents();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Wolverine host not started — integration events will be skipped. This is expected during data seeding or --migrate mode.")]
    private partial void LogSkippingIntegrationEvents();
}
