using Granit.Dashboards.Domain;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// Renders a persisted <see cref="Dashboard"/> aggregate into a wire-shaped
/// bundle, dispatching each widget through its registered
/// <see cref="IWidgetInstanceRenderer"/>. Per ADR-039 §3 — three guarantees
/// concentrated here:
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>Permission gate.</b> Widgets carrying a <c>RequiredPermission</c> the user lacks short-circuit to <c>Unavailable</c> BEFORE the typed renderer runs — story B4 acceptance criterion 4 (a widget pointing at an unreadable metric returns <c>WidgetUnavailable</c>; the dashboard renders without it; no 403 for the whole request).</item>
///   <item><b>Error isolation.</b> A bad config row, an unloaded module, or an unhandled exception in one renderer never kills the whole render. The dashboard always returns 200 with one or more widgets in <c>Error</c> / <c>Unavailable</c> state.</item>
///   <item><b>Deterministic ordering.</b> Widgets stream out in <see cref="WidgetInstance.Position"/> order, regardless of which renderer is faster — the frontend reconciles future incremental push messages by widget id, not position.</item>
/// </list>
/// </remarks>
public interface IDashboardRenderer
{
    /// <summary>
    /// Renders the supplied dashboard against <paramref name="context"/>, returning
    /// one envelope per widget plus the surrounding bundle metadata.
    /// </summary>
    /// <param name="dashboard">The dashboard aggregate (already loaded from persistence).</param>
    /// <param name="context">Per-render context — see <see cref="WidgetRenderContext"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DashboardRenderResult> RenderAsync(
        Dashboard dashboard,
        WidgetRenderContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a dashboard render — the per-widget envelopes plus the timestamp
/// of the bundle. Endpoint-level wire DTOs (e.g. <c>DashboardRenderResponse</c>)
/// project this onto whatever shape the HTTP response demands; the rendering
/// pipeline itself never mints HTTP DTOs.
/// </summary>
/// <param name="DashboardId">Dashboard identifier — matches <see cref="Dashboard.Id"/>.</param>
/// <param name="RenderedAt">Server-side timestamp at which the bundle was composed.</param>
/// <param name="Period">The resolved period that flowed into <see cref="WidgetRenderContext.Period"/>; surfaced here so the wire response can echo it back to the client without re-resolving.</param>
/// <param name="Widgets">One envelope per widget, ordered by <see cref="WidgetInstance.Position"/>. Each tuple carries the widget id alongside its envelope so the wire layer can flatten without holding both collections.</param>
public sealed record DashboardRenderResult(
    Guid DashboardId,
    DateTimeOffset RenderedAt,
    Granit.Analytics.ResolvedPeriod? Period,
    IReadOnlyList<RenderedWidget> Widgets);

/// <summary>
/// One widget's contribution to the dashboard render — the persisted id paired
/// with the typed renderer's envelope. Wire layer flattens both into the
/// outbound DTO.
/// </summary>
/// <param name="WidgetId">Persisted <see cref="WidgetInstance.Id"/>.</param>
/// <param name="Envelope">Renderer output (Snapshot / Unavailable / Error).</param>
public sealed record RenderedWidget(Guid WidgetId, WidgetSnapshotEnvelope Envelope);
