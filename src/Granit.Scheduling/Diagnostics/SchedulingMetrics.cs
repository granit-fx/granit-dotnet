using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Scheduling.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the scheduling module.
/// Meter: <c>Granit.Scheduling</c>.
/// </summary>
public sealed class SchedulingMetrics
{
    /// <summary>The meter name used for all scheduling metrics.</summary>
    public const string MeterName = "Granit.Scheduling";

    private readonly Counter<long> _actionsScheduled;
    private readonly Counter<long> _actionsExecuted;
    private readonly Counter<long> _actionsCancelled;
    private readonly Counter<long> _actionsFailed;
    private readonly Counter<long> _actionsRescheduled;
    private readonly Counter<long> _catchUpRedispatched;

    /// <summary>
    /// Initializes scheduling metrics using the specified meter factory.
    /// </summary>
    public SchedulingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _actionsScheduled = meter.CreateCounter<long>(
            "granit.scheduling.action.scheduled",
            description: "Number of scheduled actions created.");

        _actionsExecuted = meter.CreateCounter<long>(
            "granit.scheduling.action.executed",
            description: "Number of scheduled actions executed successfully.");

        _actionsCancelled = meter.CreateCounter<long>(
            "granit.scheduling.action.cancelled",
            description: "Number of scheduled actions cancelled.");

        _actionsFailed = meter.CreateCounter<long>(
            "granit.scheduling.action.failed",
            description: "Number of scheduled actions that failed.");

        _actionsRescheduled = meter.CreateCounter<long>(
            "granit.scheduling.action.rescheduled",
            description: "Number of scheduled actions rescheduled.");

        _catchUpRedispatched = meter.CreateCounter<long>(
            "granit.scheduling.catchup.redispatched",
            description: "Number of overdue actions re-dispatched by the catch-up job.");
    }

    /// <summary>Records a new scheduled action creation.</summary>
    public void RecordScheduled(string? tenantId, string payloadType)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "payload_type", payloadType },
        };
        _actionsScheduled.Add(1, tags);
    }

    /// <summary>Records a successful action execution.</summary>
    public void RecordExecuted(string? tenantId, string payloadType)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "payload_type", payloadType },
        };
        _actionsExecuted.Add(1, tags);
    }

    /// <summary>Records an action cancellation.</summary>
    public void RecordCancelled(string? tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId ?? "global" } };
        _actionsCancelled.Add(1, tags);
    }

    /// <summary>Records a failed action execution.</summary>
    public void RecordFailed(string? tenantId, string payloadType)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "payload_type", payloadType },
        };
        _actionsFailed.Add(1, tags);
    }

    /// <summary>Records an action reschedule.</summary>
    public void RecordRescheduled(string? tenantId)
    {
        var tags = new TagList { { "tenant_id", tenantId ?? "global" } };
        _actionsRescheduled.Add(1, tags);
    }

    /// <summary>Records a catch-up re-dispatch.</summary>
    public void RecordCatchUpRedispatched(string? tenantId, string payloadType)
    {
        var tags = new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "payload_type", payloadType },
        };
        _catchUpRedispatched.Add(1, tags);
    }
}
