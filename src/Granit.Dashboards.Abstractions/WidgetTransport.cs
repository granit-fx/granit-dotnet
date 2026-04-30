namespace Granit.Dashboards;

/// <summary>
/// Effective transport selected for a widget at render time. Output of
/// <see cref="DashboardPushPolicyComposer.Compose"/> — the composition of
/// the dashboard's <see cref="DashboardPushPolicy"/> and the widget's
/// <c>RefreshHint</c>.
/// </summary>
/// <remarks>
/// Surfaced on the wire under <c>DashboardRenderedWidgetResponse.Transport</c> so
/// the frontend opens push subscriptions only for widgets that actually receive
/// live updates. Pull-only widgets continue to drive their TanStack cache cadence
/// from <c>RefreshHint</c>. ADR-043 §2.3.
/// </remarks>
public enum WidgetTransport
{
    /// <summary>
    /// The widget renders over the pull endpoint. Frontend polls per
    /// <c>RefreshHint</c> TTL — never opens a stream for it.
    /// </summary>
    Pull = 0,

    /// <summary>
    /// The widget is live — frontend should open a push subscription
    /// (<c>GET /dashboards/{id}/stream</c>) and stop pulling. The pull endpoint
    /// still returns the seed envelope so the dashboard renders before the
    /// stream connects.
    /// </summary>
    Push = 1,
}
