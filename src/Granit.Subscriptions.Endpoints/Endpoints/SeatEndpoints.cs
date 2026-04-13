using Granit.Guids;
using Granit.Http.Idempotency.Attributes;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Subscriptions.Endpoints.Permissions;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Subscriptions.Endpoints.Endpoints;

internal static class SeatEndpoints
{
    internal static RouteGroupBuilder MapSeatEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/subscriptions/{id:guid}/seats", ListSeatsAsync)
            .WithName("ListSeats")
            .WithSummary("Returns all seat assignments for a subscription.")
            .WithDescription("Lists users assigned to seats on the specified subscription.")
            .Produces<IReadOnlyList<SeatResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Seats.Read);

        group.MapPost("/subscriptions/{id:guid}/seats", AssignSeatAsync)
            .WithName("AssignSeat")
            .WithSummary("Assigns a seat to a user.")
            .WithDescription("Adds a user to the subscription. Fails if the seat limit is reached.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<SeatResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Seats.Manage);

        group.MapDelete("/subscriptions/{id:guid}/seats/{userId:guid}", RevokeSeatAsync)
            .WithName("RevokeSeat")
            .WithSummary("Revokes a seat from a user.")
            .WithDescription("Removes the user's seat assignment from the subscription.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Seats.Manage);

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<SeatResponse>>, ProblemHttpResult>> ListSeatsAsync(
        Guid id,
        [FromServices] ISubscriptionReader reader,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok<IReadOnlyList<SeatResponse>>(
            sub.Seats.Select(SeatResponse.FromEntity).ToList());
    }

    private static async Task<Results<Created<SeatResponse>, ProblemHttpResult>> AssignSeatAsync(
        Guid id,
        SeatAssignRequest request,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISubscriptionWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        try
        {
            var seat = SubscriptionSeat.Create(guidGenerator.Create(), request.UserId, clock.Now);
            sub.AssignSeat(seat);
            await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);

            return TypedResults.Created(
                $"/subscriptions/{id}/seats/{request.UserId}",
                SeatResponse.FromEntity(seat));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeSeatAsync(
        Guid id,
        Guid userId,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISubscriptionWriter writer,
        CancellationToken cancellationToken)
    {
        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (!sub.RevokeSeat(userId))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        await writer.UpdateAsync(sub, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
