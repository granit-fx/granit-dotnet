using Granit.Events.Diagnostics;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IDistributedEventBus"/>.
/// Publishes integration events via <see cref="IMessageBus"/> with outbox support
/// for at-least-once delivery across service boundaries.
/// </summary>
/// <remarks>
/// When the host has not yet started (data seeding, <c>--migrate</c> mode),
/// distributed events are silently skipped with a warning log. Seeding creates
/// initial state — there are no downstream consumers that need to react to it.
/// </remarks>
internal sealed partial class WolverineDistributedEventBus(
    IMessageBus bus,
    WolverineHostReadiness readiness,
    EventsMetrics metrics,
    ILogger<WolverineDistributedEventBus> logger) : IDistributedEventBus
{
    private int _skipWarned;

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (readiness.IsReady)
        {
            await bus.PublishAsync(integrationEvent).ConfigureAwait(false);
            return;
        }

        // Wolverine not started — skip distributed events (no consumers during seeding/migrate)
        metrics.RecordEventPublished(null, "distributed-skipped", typeof(TEvent).Name);

        if (Interlocked.CompareExchange(ref _skipWarned, 1, 0) == 0)
        {
            LogSkippingDistributedEvents();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Wolverine host not started — distributed events will be skipped. This is expected during data seeding or --migrate mode.")]
    private partial void LogSkippingDistributedEvents();
}
