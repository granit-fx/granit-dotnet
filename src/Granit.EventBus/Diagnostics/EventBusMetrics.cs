using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.EventBus.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the event bus module.
/// Meter: <c>Granit.EventBus</c>.
/// </summary>
public sealed class EventBusMetrics
{
    public const string MeterName = "Granit.EventBus";

    private readonly Counter<long> _eventsPublished;
    private readonly Counter<long> _handlersExecuted;

    public EventBusMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _eventsPublished = meter.CreateCounter<long>(
            "granit.eventbus.events.published",
            description: "Number of events published via the event bus.");

        _handlersExecuted = meter.CreateCounter<long>(
            "granit.eventbus.handlers.executed",
            description: "Number of event handlers executed.");
    }

    public void RecordEventPublished(string? tenantId, string busType, string eventType) =>
        _eventsPublished.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "bus_type", busType },
            { "event_type", eventType },
        });

    public void RecordHandlerExecuted(string? tenantId, string eventType, string status) =>
        _handlersExecuted.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "event_type", eventType },
            { "status", status },
        });
}
