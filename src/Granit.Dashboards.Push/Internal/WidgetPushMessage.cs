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
/// <param name="TenantId">Owning tenant (matches the publisher's tenant scope).</param>
/// <param name="DashboardId">Dashboard the widget belongs to — partitions streams.</param>
/// <param name="WidgetInstanceId">Persisted <c>WidgetInstance.Id</c>.</param>
/// <param name="RequiredPermission">Per-widget permission gate; <see langword="null"/> when broadly readable. Filtered server-side by the SSE handler so non-entitled subscribers receive an <c>Unavailable</c> envelope instead of the live snapshot.</param>
/// <param name="StreamCursor">
/// Per-<c>(tenantId, dashboardId)</c> monotonic stream-level counter (distinct from
/// <see cref="WidgetSnapshotEnvelope.Sequence"/> which is per-widget). Surfaced on the
/// SSE <c>id:</c> field so reconnecting clients can resume via the standard
/// <c>Last-Event-ID</c> header. ADR-043 §5.
/// </param>
/// <param name="Envelope">The pull-shape envelope produced by the renderer or producer.</param>
internal sealed record WidgetPushMessage(
    Guid? TenantId,
    Guid DashboardId,
    Guid WidgetInstanceId,
    string? RequiredPermission,
    long StreamCursor,
    WidgetSnapshotEnvelope Envelope);
