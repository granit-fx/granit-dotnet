using System.Reflection;
using Granit.Events.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Events.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="IDomainEventDispatcher"/>.
/// Publishes each domain event via <see cref="IMessageBus"/> using the non-generic
/// overload so Wolverine routes by runtime type to locally discovered handlers.
/// </summary>
/// <remarks>
/// When the host has not yet started (data seeding, <c>--migrate</c> mode),
/// domain events are dispatched directly to <see cref="ILocalEventHandler{TEvent}"/>
/// handlers resolved from the scoped DI container, bypassing the Wolverine pipeline.
/// This preserves local side-effects (cache invalidation, denormalization) during seeding.
/// </remarks>
internal sealed partial class WolverineDomainEventDispatcher(
    IMessageBus bus,
    IServiceProvider serviceProvider,
    WolverineHostReadiness readiness,
    EventsMetrics metrics,
    ILogger<WolverineDomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private int _fallbackWarned;

    /// <inheritdoc/>
    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        if (readiness.IsReady)
        {
            foreach (IDomainEvent evt in domainEvents)
            {
                await bus.PublishAsync(evt).ConfigureAwait(false);
            }

            return;
        }

        // Wolverine not started — fall back to direct handler invocation.
        // serviceProvider is the SCOPED provider (this class is registered Scoped),
        // so handlers share the same scope as the caller (same DbContext instance).
        //
        // SECURITY INVARIANT: this path bypasses the Wolverine middleware pipeline.
        // Handlers invoked here MUST NOT rely on Wolverine middleware for authorization
        // or tenant context — they must receive all required context via the event payload.
        if (Interlocked.CompareExchange(ref _fallbackWarned, 1, 0) == 0)
        {
            LogFallbackActivated();
        }

        foreach (IDomainEvent evt in domainEvents)
        {
            await DispatchToLocalHandlersAsync(evt, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchToLocalHandlersAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Resolve ILocalEventHandler<TEvent> using the runtime type.
        // Domain events are dispatched via the non-generic IDomainEvent, so we need
        // to build the generic handler type from the concrete event type.
        Type eventType = domainEvent.GetType();
        string eventTypeName = eventType.Name;
        metrics.RecordEventPublished(null, "domain-fallback", eventTypeName);

        Type handlerType = typeof(ILocalEventHandler<>).MakeGenericType(eventType);
        IEnumerable<object?> handlers = serviceProvider.GetServices(handlerType);

        foreach (object? handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }

            try
            {
                // Invoke HandleAsync via the interface method
                var task = (Task)handlerType
                    .GetMethod(nameof(ILocalEventHandler<IDomainEvent>.HandleAsync))!
                    .Invoke(handler, [domainEvent, cancellationToken])!;
                await task.ConfigureAwait(false);
                metrics.RecordHandlerExecuted(null, eventTypeName, "success");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null and not OperationCanceledException)
            {
                // Unwrap reflection wrapper to log the actual handler exception
                metrics.RecordHandlerExecuted(null, eventTypeName, "error");
                LogFallbackHandlerFailed(eventType.Name, handler.GetType().Name, ex.InnerException);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                metrics.RecordHandlerExecuted(null, eventTypeName, "error");
                LogFallbackHandlerFailed(eventType.Name, handler.GetType().Name, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Wolverine host not started — domain events will be dispatched directly to local handlers (bypassing Wolverine pipeline). This is expected during data seeding or --migrate mode.")]
    private partial void LogFallbackActivated();

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Domain event handler {HandlerName} failed for event {EventName} during pre-host fallback dispatch.")]
    private partial void LogFallbackHandlerFailed(string eventName, string handlerName, Exception exception);
}
