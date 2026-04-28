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
    /// Maps the Granit.Dashboards endpoints onto <paramref name="endpoints"/>. The
    /// surface is split across three OpenAPI sub-tags per CLAUDE.md tagging
    /// convention: <c>Dashboards - Catalogue</c> for the read-only catalogue,
    /// <c>Dashboards - Instances</c> for the persisted aggregate (import, list,
    /// read, edit, state transitions), and <c>Dashboards - Widgets</c> for the
    /// widget-pool CRUD.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="DashboardsEndpointsOptions"/>.</param>
    /// <returns>The outer <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitDashboards(
        this IEndpointRouteBuilder endpoints,
        Action<DashboardsEndpointsOptions>? configure = null)
    {
        DashboardsEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        // Catalogue — read-only view of registered DashboardDefinitions.
        RouteGroupBuilder catalogGroup = group.MapGranitGroup("")
            .WithTags(options.CatalogTagName);
        catalogGroup.MapCatalogEndpoints();

        // Instances — the persisted Dashboard aggregate (import + CRUD on metadata + state machine).
        RouteGroupBuilder instancesGroup = group.MapGranitGroup("")
            .WithTags(options.InstancesTagName);
        instancesGroup.MapImportEndpoints();
        instancesGroup.MapInstanceEndpoints();
        instancesGroup.MapMetadataEditEndpoints();
        instancesGroup.MapStateTransitionEndpoints();

        // Widgets — pool CRUD scoped under /{id}/widgets.
        RouteGroupBuilder widgetsGroup = group.MapGranitGroup("")
            .WithTags(options.WidgetsTagName);
        widgetsGroup.MapWidgetEndpoints();

        return group;
    }
}
