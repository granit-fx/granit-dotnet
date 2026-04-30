using Granit.Dashboards;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Endpoints;

/// <summary>
/// HTTP handlers for the dashboard catalogue read surface.
/// </summary>
internal static class DashboardCatalogEndpoints
{
    /// <summary>
    /// Maps <c>GET /catalog</c> on the supplied route group. Returns every registered
    /// <see cref="DashboardDefinition"/> projected through <see cref="DashboardCatalogEntryResponse"/>,
    /// optionally filtered by <see cref="DashboardCategory"/>. Ordering matches the
    /// registry's stable ordering (category, then name).
    /// </summary>
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/catalog", ListCatalog)
            .WithName("ListGranitDashboardCatalog")
            .WithSummary("Lists every registered DashboardDefinition.")
            .WithDescription(
                "Returns the full dashboard catalogue surfaced by IDashboardDefinitionRegistry. "
                + "Each entry carries the wire identifier, category, version, widget count and "
                + "feature flags (HasViews / HasAliases / HasFilters) that the frontend uses to "
                + "decide how to render the import dialog. Optionally filters by ?category=... "
                + "to narrow to a specific section.")
            .RequireAuthorization(DashboardsPermissions.Catalog.Read)
            .Produces<IReadOnlyList<DashboardCatalogEntryResponse>>();

        return group;
    }

    private static Ok<IReadOnlyList<DashboardCatalogEntryResponse>> ListCatalog(
        [FromServices] IDashboardDefinitionRegistry registry,
        [FromQuery] DashboardCategory? category)
        => TypedResults.Ok(DashboardCatalogProjection.Project(registry.GetAll(), category));
}
