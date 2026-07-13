using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Notifications.Sse.StackExchangeRedis.Diagnostics;

/// <summary>Metrics for the SSE Redis backplane.</summary>
internal sealed class SseRedisBackplaneMetrics
{
    private readonly Counter<long> _published;
    private readonly Counter<long> _delivered;
    private readonly Counter<long> _dropped;

    public SseRedisBackplaneMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create("Granit.Notifications.Sse");
        _published = meter.CreateCounter<long>("granit.notifications.sse.backplane.message_published");
        _delivered = meter.CreateCounter<long>("granit.notifications.sse.backplane.message_delivered");
        _dropped = meter.CreateCounter<long>("granit.notifications.sse.backplane.message_dropped");
    }

    public void RecordPublished() => _published.Add(1, new TagList { { "tenant_id", "global" } });

    public void RecordDelivered() => _delivered.Add(1, new TagList { { "tenant_id", "global" } });

    /// <summary>A malformed envelope was logged and skipped — the listen loop survived.</summary>
    public void RecordDropped(string reason) =>
        _dropped.Add(1, new TagList { { "tenant_id", "global" }, { "reason", reason } });
}
