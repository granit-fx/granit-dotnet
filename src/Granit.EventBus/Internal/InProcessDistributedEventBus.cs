using Granit.Core.Events;
using Granit.EventBus.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.EventBus.Internal;

/// <summary>
/// In-process implementation of <see cref="IDistributedEventBus"/>.
/// Resolves all <see cref="IDistributedEventHandler{TEvent}"/> from DI and calls them sequentially.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not durable.</b> Events are lost if the process crashes before handlers complete.
/// Use only for development and testing.
/// </para>
/// <para>
/// Replaced by <c>WolverineDistributedEventBus</c> when <c>Granit.EventBus.Wolverine</c>
/// is loaded, which provides outbox-backed at-least-once delivery.
/// </para>
/// </remarks>
internal sealed partial class InProcessDistributedEventBus(
    IServiceProvider serviceProvider,
    ILogger<InProcessDistributedEventBus> logger,
    EventBusMetrics metrics) : IDistributedEventBus
{
    private bool _warned;

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!_warned)
        {
            LogNotDurable();
            _warned = true;
        }

        string eventType = typeof(TEvent).Name;
        metrics.RecordEventPublished(null, "distributed", eventType);

        IEnumerable<IDistributedEventHandler<TEvent>> handlers =
            serviceProvider.GetServices<IDistributedEventHandler<TEvent>>();

        foreach (IDistributedEventHandler<TEvent> handler in handlers)
        {
            try
            {
                await handler.HandleAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
                metrics.RecordHandlerExecuted(null, eventType, "success");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                metrics.RecordHandlerExecuted(null, eventType, "error");
                LogHandlerFailed(typeof(TEvent).Name, handler.GetType().Name, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "InProcessDistributedEventBus is not durable — events will be lost on crash. Use Granit.EventBus.Wolverine for production")]
    private partial void LogNotDurable();

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Distributed event handler {HandlerName} failed for event {EventName}")]
    private partial void LogHandlerFailed(string eventName, string handlerName, Exception exception);
}
