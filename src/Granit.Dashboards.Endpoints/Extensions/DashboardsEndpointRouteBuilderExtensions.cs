using Granit.Dashboards.Endpoints.Endpoints;
using Granit.Dashboards.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering Granit.Dashboards HTTP endpoints.
/// </summary>
public static class DashboardsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the Granit.Dashboards endpoints onto <paramref name="endpoints"/>. Today
    /// ships the read-only catalogue endpoint and the import endpoint
    /// (<c>POST /from-definition/{name}</c>); the list / read / state-transition
    /// endpoints land on the same route group in subsequent stories.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="DashboardsEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitDashboards(
        this IEndpointRouteBuilder endpoints,
        Action<DashboardsEndpointsOptions>? configure = null)
    {
        DashboardsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.CatalogTagName);

        group.MapCatalogEndpoints();
        group.MapImportEndpoints();
        group.MapInstanceEndpoints();
        group.MapMetadataEditEndpoints();
        group.MapStateTransitionEndpoints();

        return group;
    }
}
