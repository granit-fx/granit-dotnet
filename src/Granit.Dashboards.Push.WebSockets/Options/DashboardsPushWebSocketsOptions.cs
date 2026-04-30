using System.ComponentModel.DataAnnotations;

namespace Granit.Dashboards.Push.WebSockets.Options;

/// <summary>
/// Wire / runtime knobs for <c>Granit.Dashboards.Push.WebSockets</c>. The SSE
/// transport's <c>DashboardsPushOptions</c> stays the source of truth for
/// shared concerns (heartbeat cadence, ring buffer capacity); this record
/// only carries the WebSocket-specific knobs.
/// </summary>
public sealed class DashboardsPushWebSocketsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Granit:Dashboards:PushWebSockets";

    /// <summary>
    /// Route prefix for the WebSocket endpoint group. The endpoint pattern is
    /// <c>{prefix}/{id:guid}/stream-ws</c>. Defaults to <c>"dashboards"</c> to
    /// align with <c>Granit.Dashboards.Endpoints</c>'s default prefix.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string RoutePrefix { get; set; } = "dashboards";

    /// <summary>
    /// OpenAPI tag for the WebSocket endpoint group. Default:
    /// <c>"Dashboards - Stream (WebSocket)"</c> so the Scalar UI groups it
    /// alongside the SSE stream tag without conflating the two.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string TagName { get; set; } = "Dashboards - Stream (WebSocket)";

    /// <summary>
    /// Maximum time the framework waits for the client's optional opening
    /// frame carrying <c>{ "lastEventId": N }</c> before subscribing fresh.
    /// Default: 1 second — generous enough for slow networks, tight enough to
    /// keep stalled handshakes from blocking the request thread.
    /// </summary>
    public TimeSpan OpeningFrameTimeout { get; set; } = TimeSpan.FromSeconds(1);
}
