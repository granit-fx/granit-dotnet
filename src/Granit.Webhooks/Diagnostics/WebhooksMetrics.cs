using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Webhooks.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the webhooks module.
/// Meter: <c>Granit.Webhooks</c>.
/// </summary>
public sealed class WebhooksMetrics
{
    public const string MeterName = "Granit.Webhooks";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";
    private const string TagEventType = "event_type";

    private readonly Counter<long> _fanoutTriggered;
    private readonly Counter<long> _deliveriesSucceeded;
    private readonly Counter<long> _deliveriesFailed;
    private readonly Counter<long> _subscriptionsSuspended;
    private readonly Histogram<double> _deliveryDuration;

    public WebhooksMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _fanoutTriggered = meter.CreateCounter<long>(
            "granit.webhooks.fanout.triggered",
            description: "Number of webhook fan-out operations triggered.");

        _deliveriesSucceeded = meter.CreateCounter<long>(
            "granit.webhooks.deliveries.succeeded",
            description: "Number of webhook deliveries that succeeded (2xx response).");

        _deliveriesFailed = meter.CreateCounter<long>(
            "granit.webhooks.deliveries.failed",
            description: "Number of webhook deliveries that failed (non-2xx or timeout).");

        _subscriptionsSuspended = meter.CreateCounter<long>(
            "granit.webhooks.subscriptions.suspended",
            description: "Number of webhook subscriptions auto-suspended due to persistent errors.");

        _deliveryDuration = meter.CreateHistogram<double>(
            "granit.webhooks.delivery.duration",
            unit: "s",
            description: "Duration of webhook delivery HTTP calls in seconds.");
    }

    public void RecordFanoutTriggered(string? tenantId, string eventType) =>
        _fanoutTriggered.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEventType, eventType },
        });

    public void RecordDeliverySucceeded(string? tenantId, string eventType) =>
        _deliveriesSucceeded.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEventType, eventType },
        });

    public void RecordDeliveryFailed(string? tenantId, string eventType, int? httpStatus) =>
        _deliveriesFailed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEventType, eventType },
            { "http_status", httpStatus?.ToString() ?? "timeout" },
        });

    public void RecordSubscriptionSuspended(string? tenantId, int httpStatus) =>
        _subscriptionsSuspended.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "http_status", httpStatus.ToString() },
        });

    public void RecordDeliveryDuration(string? tenantId, string eventType, string status, TimeSpan duration) =>
        _deliveryDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEventType, eventType },
            { "status", status },
        });
}
