using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Dashboards.Endpoints.Endpoints;

/// <summary>
/// HTTP handler for <c>POST /dashboards/{id}/resync</c> — replays the
/// registered <c>DashboardDefinition</c> behind a persisted <see cref="Dashboard"/>'s
/// <see cref="Dashboard.SourceDefinitionName"/> through the aggregate's
/// <see cref="Dashboard.Resync"/> behaviour. ADR-038 §3 closes the drift loop:
/// the render endpoint tells the frontend a dashboard is <c>Behind</c> /
/// <c>Ahead</c>, this endpoint is the action the "click to resync" affordance
/// dispatches.
/// </summary>
internal static class DashboardResyncEndpoints
{
    public static RouteGroupBuilder MapResyncEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/resync", ResyncAsync)
            .WithName("ResyncDashboard")
            .WithSummary("Resyncs a persisted dashboard with its currently-registered DashboardDefinition.")
            .WithDescription(
                "Replaces the widget pool with the descriptor's current entry-view widgets, "
                + "refreshes the structural metadata (layout, isSystem, sourceDefinitionVersion), "
                + "and best-effort preserves per-instance overrides via Widget:{Name}.{slug} match. "
                + "The dashboard's user-renamable Name and lifecycle Status are intentionally "
                + "preserved. Returns 200 with a change-summary, 404 when the dashboard is not "
                + "found in the current tenant scope, or 409 when resync is not applicable "
                + "(ad-hoc dashboard, or source definition no longer registered in the host).")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<DashboardResyncResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<Ok<DashboardResyncResponse>, ProblemHttpResult>> ResyncAsync(
        [FromRoute] Guid id,
        [FromServices] DashboardResyncer resyncer,
        CancellationToken cancellationToken)
    {
        DashboardResyncResult result = await resyncer.ResyncAsync(id, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            DashboardResyncOutcome.Updated => TypedResults.Ok(new DashboardResyncResponse(
                Id: result.Dashboard!.Id,
                Name: result.Dashboard.Name,
                Status: result.Dashboard.Status,
                SourceDefinitionName: result.Dashboard.SourceDefinitionName!,
                PreviousSourceDefinitionVersion: result.Summary!.PreviousSourceDefinitionVersion,
                SourceDefinitionVersion: result.Summary.NewSourceDefinitionVersion,
                WidgetsAdded: result.Summary.WidgetsAdded,
                WidgetsRemoved: result.Summary.WidgetsRemoved,
                OverridesCarriedOver: result.Summary.OverridesCarriedOver)),
            DashboardResyncOutcome.NotFound => TypedResults.Problem(
                detail: $"Dashboard '{id}' not found.",
                statusCode: StatusCodes.Status404NotFound),
            DashboardResyncOutcome.NotApplicable or DashboardResyncOutcome.SourceUnregistered => TypedResults.Problem(
                detail: result.ConflictReason,
                statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Problem(
                detail: "Unhandled resync outcome.",
                statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
