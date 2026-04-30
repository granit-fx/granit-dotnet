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

    /// <summary>
    /// Per-stream ring-buffer capacity used by the in-memory hub for
    /// <c>Last-Event-ID</c> resume (ADR-043 §5). When a reconnecting client
    /// requests envelopes older than the ring's oldest entry, the SSE handler
    /// emits <c>event: resume-failed</c> and the frontend hook re-fetches the
    /// seed via the pull endpoint. Default: 100 — sized so a 30 s reconnect
    /// gap on a busy 3-Hz dashboard still resumes cleanly.
    /// </summary>
    [Range(1, 100_000)]
    public int RingBufferCapacity { get; set; } = 100;
}
