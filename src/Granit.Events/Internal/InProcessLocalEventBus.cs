using Granit.Events;
using Granit.Events.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Events.Internal;

/// <summary>
/// In-process implementation of <see cref="ILocalEventBus"/>.
/// Resolves all <see cref="ILocalEventHandler{TEvent}"/> from DI and calls them sequentially.
/// </summary>
/// <remarks>
/// No durability, no retry. Suitable for monoliths and tests.
/// Replaced by <c>WolverineLocalEventBus</c> when <c>Granit.Events.Wolverine</c> is loaded.
/// </remarks>
internal sealed partial class InProcessLocalEventBus(
    IServiceProvider serviceProvider,
    ILogger<InProcessLocalEventBus> logger,
    EventsMetrics metrics) : ILocalEventBus
{
    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(localEvent);

        string eventType = typeof(TEvent).Name;
        metrics.RecordEventPublished(null, "local", eventType);

        IEnumerable<ILocalEventHandler<TEvent>> handlers =
            serviceProvider.GetServices<ILocalEventHandler<TEvent>>();

        foreach (ILocalEventHandler<TEvent> handler in handlers)
        {
            try
            {
                await handler.HandleAsync(localEvent, cancellationToken).ConfigureAwait(false);
                metrics.RecordHandlerExecuted(null, eventType, "success");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                metrics.RecordHandlerExecuted(null, eventType, "error");
                LogHandlerFailed(typeof(TEvent).Name, handler.GetType().Name, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Local event handler {HandlerName} failed for event {EventName}")]
    private partial void LogHandlerFailed(string eventName, string handlerName, Exception exception);
}
