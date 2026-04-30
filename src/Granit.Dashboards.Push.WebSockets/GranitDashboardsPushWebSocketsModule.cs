using Granit.Dashboards.Push.WebSockets.Extensions;
using Granit.Modularity;

namespace Granit.Dashboards.Push.WebSockets;

/// <summary>
/// Granit module for the WebSocket dashboards push transport. Sibling of
/// <c>GranitDashboardsPushModule</c> — same producer contract, alternate
/// consumer protocol. ADR-043.
/// </summary>
/// <remarks>
/// Hosts that prefer (or also need) WebSocket clients add this module to their
/// composition root and call <c>app.MapGranitDashboardsPushWebSockets()</c>.
/// The hub registered by <c>GranitDashboardsPushModule</c> is reused as-is —
/// no separate publisher wiring needed.
/// </remarks>
[DependsOn(typeof(GranitDashboardsPushModule))]
public sealed class GranitDashboardsPushWebSocketsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDashboardsPushWebSockets();
}
