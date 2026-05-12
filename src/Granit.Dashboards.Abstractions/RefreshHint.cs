namespace Granit.Dashboards;

/// <summary>
/// Indicates how often a metric's underlying data is expected to change. Drives the
/// caching layer's TTL and signals to the dashboards transport layer whether the
/// widget is push-eligible.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Static"/> and <see cref="Dynamic"/> drive FusionCache TTL on the pull
/// path. <see cref="Realtime"/> declares the metric as push-eligible — the framework
/// transport (<c>Granit.Dashboards.Push</c>, ADR-043) wires a live channel for the
/// widget when its dashboard's effective <c>DashboardPushPolicy</c> opts in. Hosts
/// that haven't loaded the push transport package fall back to the
/// <see cref="Dynamic"/> pull cadence — no widget breaks at runtime when the
/// transport is absent.
/// </para>
/// </remarks>
public enum RefreshHint
{
    /// <summary>Long-lived data (5+ min cache acceptable). E.g. tenant settings, role catalog.</summary>
    Static,

    /// <summary>Short-lived but pull-friendly (60–120 s cache). Default for most KPIs.</summary>
    Dynamic,

    /// <summary>
    /// Push-eligible — sub-second updates. Wired to the live channel by
    /// <c>Granit.Dashboards.Push</c> (ADR-043). Falls back to <see cref="Dynamic"/>
    /// pull cadence when the host hasn't loaded the push transport package.
    /// </summary>
    Realtime,
}
