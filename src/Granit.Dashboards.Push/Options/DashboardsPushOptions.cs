using System.ComponentModel.DataAnnotations;

namespace Granit.Dashboards.Push.Options;

/// <summary>
/// Wire / runtime knobs for <c>Granit.Dashboards.Push</c>. Bound from
/// configuration when <c>AddGranitDashboardsPush</c> is wired with the
/// <c>IConfiguration</c> overload.
/// </summary>
public sealed class DashboardsPushOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Granit:Dashboards:Push";

    /// <summary>
    /// Route prefix for the dashboards-push endpoint group. Mounts under the
    /// host's existing dashboards prefix at runtime (the SSE endpoint pattern
    /// is <c>{prefix}/{id:guid}/stream</c>). Defaults to <c>"dashboards"</c>
    /// to align with <c>Granit.Dashboards.Endpoints</c>'s default prefix.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string RoutePrefix { get; set; } = "dashboards";

    /// <summary>
    /// Heartbeat cadence for the SSE stream. The framework writes a comment
    /// frame every <see cref="HeartbeatInterval"/> so reverse proxies and CDNs
    /// keep the connection alive when no widget is publishing. Default: 15 s.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// OpenAPI tag for the push endpoint group. Default: <c>"Dashboards - Stream"</c>
    /// per the CLAUDE.md tagging convention (<c>&lt;Module&gt; - &lt;SubGroup&gt;</c>).
    /// </summary>
    [Required]
    [MinLength(1)]
    public string TagName { get; set; } = "Dashboards - Stream";
}
