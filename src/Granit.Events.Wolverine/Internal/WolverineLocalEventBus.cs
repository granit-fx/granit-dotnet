using Granit.Events.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="ILocalEventBus"/>.
/// Publishes events to a Wolverine local queue for in-process handler execution
/// with Wolverine's pipeline (validation, context propagation, retry).
/// </summary>
/// <remarks>
/// When the host has not yet started (data seeding, <c>--migrate</c> mode),
/// Wolverine's <see cref="IMessageBus"/> throws <c>WolverineHasNotStartedException</c>.
/// In that case, this implementation falls back to direct handler invocation —
/// resolving <see cref="ILocalEventHandler{TEvent}"/> from DI and calling sequentially,
/// matching <c>InProcessLocalEventBus</c> behavior.
/// </remarks>
public sealed partial class WolverineLocalEventBus(
    IMessageBus bus,
    IServiceProvider serviceProvider,
    WolverineHostReadiness readiness,
    EventsMetrics metrics,
    ILogger<WolverineLocalEventBus> logger) : ILocalEventBus
{
    private int _fallbackWarned;

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(localEvent);

        if (readiness.IsReady)
        {
            await bus.PublishAsync(localEvent).ConfigureAwait(false);
            return;
        }

        // Wolverine not started — fall back to direct handler invocation.
        // serviceProvider is the SCOPED provider (this class is registered Scoped),
        // so handlers share the same scope as the caller — preserving transactional
        // consistency (e.g., same DbContext instance). Do NOT create a new scope here.
        //
        // SECURITY INVARIANT: this path bypasses the Wolverine middleware pipeline.
        // Handlers invoked here MUST NOT rely on Wolverine middleware for authorization
        // or tenant context — they must receive all required context via the event payload.
        if (Interlocked.CompareExchange(ref _fallbackWarned, 1, 0) == 0)
        {
            LogFallbackActivated();
        }

        string eventType = typeof(TEvent).Name;
        metrics.RecordEventPublished(null, "local-fallback", eventType);

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
                LogFallbackHandlerFailed(typeof(TEvent).Name, handler.GetType().Name, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Wolverine host not started — local events will be dispatched directly to handlers (bypassing Wolverine pipeline). This is expected during data seeding or --migrate mode.")]
    private partial void LogFallbackActivated();

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Local event handler {HandlerName} failed for event {EventName} during pre-host fallback dispatch.")]
    private partial void LogFallbackHandlerFailed(string eventName, string handlerName, Exception exception);
}
