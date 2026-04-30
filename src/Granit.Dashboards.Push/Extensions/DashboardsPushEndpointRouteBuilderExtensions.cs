using Granit.Dashboards.Push.Endpoints;
using Granit.Dashboards.Push.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Dashboards.Push.Extensions;

/// <summary>
/// Endpoint registration for the dashboards push SSE stream. Hosts call
/// <c>app.MapGranitDashboardsPush()</c> after the standard
/// <c>app.MapGranitDashboards()</c> — the two share the same default route
/// prefix so the SSE endpoint lands at <c>{prefix}/{id:guid}/stream</c>
/// alongside the bundle render endpoint.
/// </summary>
public static class DashboardsPushEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>GET /{prefix}/{id:guid}/stream</c> for live widget updates.
    /// Returns the route group for further chaining. Requires the SSE handler's
    /// dependencies to be wired via <c>AddGranitDashboardsPush</c>.
    /// </summary>
    public static RouteGroupBuilder MapGranitDashboardsPush(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        DashboardsPushOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<DashboardsPushOptions>>().Value;

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);
        group.WithTags(options.TagName);
        group.MapDashboardStreamEndpoint();
        return group;
    }
}
