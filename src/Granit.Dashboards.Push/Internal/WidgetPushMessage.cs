using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// Hub-internal envelope carried between <see cref="IWidgetPushPublisher"/> and
/// each <see cref="System.Threading.Channels.ChannelWriter{T}"/> registered by
/// the SSE endpoint. The dashboard / tenant context lets the SSE handler filter
/// inbound messages to the subscription it represents — even though the hub
/// already partitions by <c>(tenantId, dashboardId)</c>, the receiver gets the
/// full identity for free.
/// </summary>
internal sealed record WidgetPushMessage(
    Guid? TenantId,
    Guid DashboardId,
    Guid WidgetInstanceId,
    string? RequiredPermission,
    WidgetSnapshotEnvelope Envelope);
