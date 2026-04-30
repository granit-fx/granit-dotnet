using Granit.Dashboards.Push.WebSockets.Endpoints;
using Granit.Dashboards.Push.WebSockets.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Dashboards.Push.WebSockets.Extensions;

/// <summary>
/// Endpoint registration for the WebSocket dashboards stream. Hosts call
/// <c>app.MapGranitDashboardsPushWebSockets()</c> alongside (or instead of)
/// <c>app.MapGranitDashboardsPush()</c>. The two transports share the in-memory
/// hub and producer contract; choose one or both per deployment.
/// </summary>
public static class DashboardsPushWebSocketsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>GET /{prefix}/{id:guid}/stream-ws</c> for live widget updates
    /// over WebSocket. Returns the route group for further chaining.
    /// </summary>
    public static RouteGroupBuilder MapGranitDashboardsPushWebSockets(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        DashboardsPushWebSocketsOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<DashboardsPushWebSocketsOptions>>().Value;

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);
        group.WithTags(options.TagName);
        group.MapDashboardWebSocketStreamEndpoint();
        return group;
    }
}
