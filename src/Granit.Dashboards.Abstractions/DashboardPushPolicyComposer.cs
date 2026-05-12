
namespace Granit.Dashboards;

/// <summary>
/// Pure composition of <see cref="DashboardPushPolicy"/> and
/// <see cref="RefreshHint"/> into the effective <see cref="WidgetTransport"/> for
/// one widget. Locked by ADR-043 §2.3 — the same matrix is shipped to the front
/// in the public docs page.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.Dashboards.Abstractions</c> so both the render-time projector
/// (<c>Granit.Dashboards.Endpoints</c>) and the future push transport hub
/// (<c>Granit.Dashboards.Push</c>) consume the exact same function — no chance for
/// the two ends to disagree on which widget is live.
/// </remarks>
public static class DashboardPushPolicyComposer
{
    /// <summary>
    /// Composes a single widget's effective transport from its dashboard's policy
    /// and its own <see cref="RefreshHint"/>. Pure — same input, same output.
    /// </summary>
    /// <param name="dashboardPolicy">Policy declared by the dashboard descriptor (or persisted on the aggregate after import).</param>
    /// <param name="widgetHint">Refresh hint surfaced by the widget renderer (typically inherited from the underlying metric / query, or hardcoded for static-content widgets).</param>
    /// <returns>
    /// <see cref="WidgetTransport.Push"/> when the widget warrants a live channel
    /// per the matrix; <see cref="WidgetTransport.Pull"/> otherwise. Static-content
    /// widgets always pull, even under <see cref="DashboardPushPolicy.Force"/> —
    /// pushing a Markdown banner has no value.
    /// </returns>
    public static WidgetTransport Compose(DashboardPushPolicy dashboardPolicy, RefreshHint widgetHint)
        => (dashboardPolicy, widgetHint) switch
        {
            (DashboardPushPolicy.PullOnly, _) => WidgetTransport.Pull,
            (DashboardPushPolicy.WhenWidgetsRequest, RefreshHint.Realtime) => WidgetTransport.Push,
            (DashboardPushPolicy.WhenWidgetsRequest, _) => WidgetTransport.Pull,
            (DashboardPushPolicy.Force, RefreshHint.Static) => WidgetTransport.Pull,
            (DashboardPushPolicy.Force, _) => WidgetTransport.Push,
            _ => WidgetTransport.Pull,
        };
}
