using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Permissions;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Endpoints;

/// <summary>
/// HTTP handler for <c>POST /dashboards/from-definition/{name}</c> — deep-copies a
/// registered <c>DashboardDefinition</c> into a tenant-scoped persisted
/// <see cref="Dashboard"/>. ADR-038 §2 import flow: explicit, never automatic.
/// </summary>
internal static class DashboardImportEndpoints
{
    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/from-definition/{name}", ImportAsync)
            .WithName("ImportGranitDashboardFromDefinition")
            .WithSummary("Imports a registered DashboardDefinition into a persisted Dashboard.")
            .WithDescription(
                "Deep-copies the named DashboardDefinition into a fresh tenant-scoped "
                + "Dashboard aggregate (status: Draft, sourceDefinitionVersion captured at "
                + "import time). Multi-view definitions yield a Dashboard whose widgets are "
                + "the entry view's pool (DefaultView, or the first view when DefaultView is "
                + "null). Returns 201 with the new dashboard's identifying fields, or 404 "
                + "when the definition name is not registered.")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<DashboardImportResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Created<DashboardImportResponse>, ProblemHttpResult>> ImportAsync(
        [FromRoute] string name,
        [FromServices] DashboardImporter importer,
        CancellationToken cancellationToken)
    {
        Dashboard? imported = await importer.ImportAsync(name, cancellationToken).ConfigureAwait(false);
        if (imported is null)
        {
            return TypedResults.Problem(
                detail: $"Dashboard definition '{name}' is not registered.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var response = new DashboardImportResponse(
            Id: imported.Id,
            Name: imported.Name,
            Category: imported.Category,
            Status: imported.Status,
            SourceDefinitionName: imported.SourceDefinitionName!,
            SourceDefinitionVersion: imported.SourceDefinitionVersion!,
            WidgetCount: imported.Widgets.Count);

        return TypedResults.Created($"/dashboards/{imported.Id}", response);
    }
}
