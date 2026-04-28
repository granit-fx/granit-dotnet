using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
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
/// HTTP handler for <c>PUT /dashboards/{id}</c> — edits the dashboard's
/// editable metadata (name + grid layout). Status, source-definition pinning,
/// and the widget pool are intentionally out of scope here; they belong to the
/// state-transition / import / widget endpoints respectively.
/// </summary>
internal static class DashboardMetadataEditEndpoints
{
    public static RouteGroupBuilder MapMetadataEditEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", UpdateMetadataAsync)
            .WithName("UpdateGranitDashboardMetadata")
            .WithSummary("Updates the dashboard's name and grid layout.")
            .WithDescription(
                "Full replacement of the editable metadata: Name, LayoutColumns and "
                + "LayoutRowHeight. FluentValidation runs first (Name not empty, layout "
                + "values > 0); the domain guards are kept as defense-in-depth and "
                + "surface as 422 if hit. Returns the updated dashboard summary so "
                + "callers refresh without a follow-up GET.")
            .RequireAuthorization(DashboardsPermissions.Instances.Manage)
            .Produces<DashboardSummaryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    private static async Task<Results<Ok<DashboardSummaryResponse>, ProblemHttpResult>> UpdateMetadataAsync(
        [FromRoute] Guid id,
        [FromBody] DashboardMetadataUpdateRequest request,
        [FromServices] DashboardEditor editor,
        CancellationToken cancellationToken)
    {
        DashboardEditResult result = await editor.UpdateMetadataAsync(
            id,
            request.Name,
            request.LayoutColumns,
            request.LayoutRowHeight,
            cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            DashboardEditOutcome.Updated =>
                TypedResults.Ok(DashboardInstanceProjection.ToSummary(result.Dashboard!)),
            DashboardEditOutcome.NotFound =>
                TypedResults.Problem(
                    detail: $"Dashboard '{id}' not found.",
                    statusCode: StatusCodes.Status404NotFound),
            DashboardEditOutcome.Invalid =>
                TypedResults.Problem(
                    detail: result.InvalidReason,
                    statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => throw new InvalidOperationException($"Unhandled outcome '{result.Outcome}'."),
        };
    }
}
