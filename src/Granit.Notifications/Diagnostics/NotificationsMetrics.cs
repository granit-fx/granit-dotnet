using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Notifications.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the notifications module.
/// Meter: <c>Granit.Notifications</c>.
/// </summary>
public sealed class NotificationsMetrics
{
    public const string MeterName = "Granit.Notifications";

    private readonly Counter<long> _fanoutTriggered;
    private readonly Counter<long> _deliveriesSucceeded;
    private readonly Counter<long> _deliveriesFailed;
    private readonly Histogram<double> _deliveryDuration;

    public NotificationsMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _fanoutTriggered = meter.CreateCounter<long>(
            "granit.notifications.fanout.triggered",
            description: "Number of notification fan-outs triggered.");

        _deliveriesSucceeded = meter.CreateCounter<long>(
            "granit.notifications.deliveries.succeeded",
            description: "Number of notification deliveries that succeeded.");

        _deliveriesFailed = meter.CreateCounter<long>(
            "granit.notifications.deliveries.failed",
            description: "Number of notification deliveries that failed.");

        _deliveryDuration = meter.CreateHistogram<double>(
            "granit.notifications.delivery.duration",
            unit: "s",
            description: "Duration of notification delivery in seconds.");
    }

    public void RecordFanoutTriggered(string? tenantId, string notificationType) =>
        _fanoutTriggered.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "notification_type", notificationType },
        });

    public void RecordDeliverySucceeded(string? tenantId, string channel, string notificationType) =>
        _deliveriesSucceeded.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "channel", channel },
            { "notification_type", notificationType },
        });

    public void RecordDeliveryFailed(string? tenantId, string channel, string notificationType) =>
        _deliveriesFailed.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "channel", channel },
            { "notification_type", notificationType },
        });

    public void RecordDeliveryDuration(string? tenantId, string channel, string status, TimeSpan duration) =>
        _deliveryDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "channel", channel },
            { "status", status },
        });
}
