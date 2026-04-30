namespace Granit.Dashboards;

/// <summary>
/// Per-dashboard switch that decides which widgets warrant a live push channel.
/// ADR-043 §2 — composed at render time with each widget's
/// <c>RefreshHint</c> to produce the effective transport.
/// </summary>
/// <remarks>
/// <para>
/// The framework evaluates the composition matrix per widget; the render bundle
/// response surfaces the chosen transport on
/// <c>DashboardRenderedWidgetResponse.Transport</c> so the frontend opens streams
/// selectively. See <see cref="DashboardPushPolicyComposer.Compose"/> for the
/// canonical resolution.
/// </para>
/// <para>
/// Pull fallback is automatic — a host that hasn't loaded the framework push
/// transport package emits <see cref="WidgetTransport.Pull"/> for every widget
/// regardless of declared policy, so dashboards keep rendering.
/// </para>
/// </remarks>
public enum DashboardPushPolicy
{
    /// <summary>
    /// Pure pull. Dashboard ignores any per-widget <c>Realtime</c> hint — every
    /// widget renders over the pull endpoint and the frontend polls per
    /// <c>RefreshHint</c> TTL. Default for verticals that prefer a predictable
    /// cost ceiling or haven't loaded the push transport package.
    /// </summary>
    PullOnly = 0,

    /// <summary>
    /// Push the widgets whose effective <c>RefreshHint</c> is <c>Realtime</c>.
    /// Pull everything else. Default for dashboards that mix live + cached data
    /// (e.g. one alerting widget on a finance overview).
    /// </summary>
    WhenWidgetsRequest = 1,

    /// <summary>
    /// Push every widget regardless of its <c>RefreshHint</c> (with the exception
    /// of static-content widgets — there's no point pushing a Markdown banner).
    /// Use sparingly — reserved for cockpit-style boards where the whole admin's
    /// mental model is "this board is live".
    /// </summary>
    Force = 2,
}
