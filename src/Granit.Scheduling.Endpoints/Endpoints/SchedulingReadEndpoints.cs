using Granit.Authorization.Extensions;
using Granit.Scheduling.Domain;
using Granit.Scheduling.Domain.ValueObjects;
using Granit.Scheduling.Endpoints.Dtos;
using Granit.Scheduling.Endpoints.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Scheduling.Endpoints.Endpoints;

/// <summary>
/// GET endpoint for viewing a single scheduled action by ID.
/// The list/search endpoint is provided by <c>MapGranitQuery&lt;ScheduledAction&gt;</c>.
/// </summary>
internal static class SchedulingReadEndpoints
{
    internal static RouteGroupBuilder MapReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", GetActionByIdAsync)
            .WithName("GetScheduledActionById")
            .WithSummary("Returns a scheduled action by ID.")
            .WithDescription("Returns the full details of a single scheduled action identified by its GUID. Returns 404 if the action does not exist or belongs to a different tenant.")
            .Produces<ScheduledActionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Results<Ok<ScheduledActionResponse>, ProblemHttpResult>> GetActionByIdAsync(
        Guid id,
        [FromServices] IScheduledActionReader reader,
        CancellationToken cancellationToken)
    {
        ScheduledAction? action = await reader.GetByIdAsync(
            ScheduledActionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (action is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(MapToResponse(action));
    }

    internal static ScheduledActionResponse MapToResponse(ScheduledAction action) =>
        new(
            action.Id,
            action.PayloadType.Split(',')[0].Split('.')[^1],
            action.ExecuteAt,
            action.CorrelationId,
            action.Status,
            action.ExecutedAt,
            action.CancelledBy,
            action.FailureReason,
            action.CreatedAt);
}
