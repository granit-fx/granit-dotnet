using System.Text.Json;
using Granit.Analytics.Metrics;

namespace Granit.Dashboards.Push;

/// <summary>
/// Producer-side contract for emitting live widget updates. ADR-043 §4.
/// Implementations route the envelope to every active subscriber on the
/// matching <c>(tenantId, dashboardId)</c> stream and allocate the next
/// monotonic <see cref="WidgetSnapshotEnvelope.Sequence"/> per
/// <c>(widgetInstanceId, tenantId)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Producers that *can't* push (their data only updates on schedule) simply
/// don't call this — the dashboard falls back to the pull cadence implied by
/// the effective transport policy (ADR-043 §2.3 + §7).
/// </para>
/// <para>
/// The default implementation registered by
/// <c>Granit.Dashboards.Push.Extensions.AddGranitDashboardsPush()</c> is an
/// in-process hub that fans out to subscribers on the same process. Multi-host
/// deployments either host one stream-handler instance, or DI-replace the hub
/// with a Redis / Wolverine-backed adapter.
/// </para>
/// </remarks>
public interface IWidgetPushPublisher
{
    /// <summary>
    /// Publishes a fresh snapshot for the given widget instance. Allocates the
    /// next sequence number on the framework's monotonic counter and dispatches
    /// the envelope to active subscribers.
    /// </summary>
    /// <param name="tenantId">Owning tenant. <see langword="null"/> for global widgets (rare — typically only a host-wide ops dashboard would use null).</param>
    /// <param name="dashboardId">Persisted dashboard identifier the widget belongs to. Streams partition by this id.</param>
    /// <param name="widgetInstanceId">Persisted <c>WidgetInstance.Id</c>.</param>
    /// <param name="widgetType">Widget kind discriminator — mirrors <c>WidgetSnapshotEnvelope.WidgetType</c>.</param>
    /// <param name="snapshot">Pre-serialized typed payload. Same shape consumers see on the pull endpoint.</param>
    /// <param name="emittedAt">Server-side timestamp of the snapshot.</param>
    /// <param name="refreshHint">Refresh hint surfaced on the wire envelope (typically inherited from the metric / query).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishSnapshotAsync(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string widgetType,
        JsonElement snapshot,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an <c>Unavailable</c> envelope for the given widget instance —
    /// the producer signals that the data is currently not available (permission
    /// gate, missing metric, transient failure). Subscribers receive the same
    /// shape as a snapshot envelope with <c>Status = Unavailable</c> and the
    /// reason key populated.
    /// </summary>
    Task PublishUnavailableAsync(
        Guid? tenantId,
        Guid dashboardId,
        Guid widgetInstanceId,
        string widgetType,
        DateTimeOffset emittedAt,
        RefreshHint refreshHint,
        string reasonLocalizationKey,
        CancellationToken cancellationToken = default);
}
