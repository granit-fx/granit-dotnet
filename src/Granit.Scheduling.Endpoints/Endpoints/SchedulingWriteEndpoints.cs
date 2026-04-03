using Granit.Scheduling.Domain.ValueObjects;
using Granit.Scheduling.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Scheduling.Endpoints.Endpoints;

/// <summary>
/// DELETE/PUT endpoints for managing scheduled actions.
/// </summary>
internal static class SchedulingWriteEndpoints
{
    internal static RouteGroupBuilder MapWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", CancelActionAsync)
            .WithName("CancelScheduledAction")
            .WithSummary("Cancels a pending scheduled action.")
            .WithDescription("Cancels a scheduled action that has not yet been executed. Returns 204 on success. Returns 404 if the action does not exist, or 409 if the action is no longer in Pending status.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/reschedule", RescheduleActionAsync)
            .WithName("RescheduleScheduledAction")
            .WithSummary("Reschedules a pending action to a new date.")
            .WithDescription("Changes the execution date of a pending scheduled action. Returns 200 on success. Returns 404 if the action does not exist, or 409 if the action is no longer in Pending status.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> CancelActionAsync(
        Guid id,
        [FromServices] IScheduler scheduler,
        CancellationToken cancellationToken)
    {
        try
        {
            await scheduler.CancelAsync(ScheduledActionId.Create(id), cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> RescheduleActionAsync(
        Guid id,
        [FromBody] RescheduleActionRequest request,
        [FromServices] IScheduler scheduler,
        CancellationToken cancellationToken)
    {
        try
        {
            await scheduler.RescheduleAsync(
                ScheduledActionId.Create(id), request.NewExecuteAt, cancellationToken).ConfigureAwait(false);
            return TypedResults.Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
