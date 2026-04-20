using Granit.Authorization.Extensions;
using Granit.Http.Idempotency.Attributes;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Subscriptions.Endpoints.Permissions;
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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Seats.Read)
            .AllowHostAccess();

        group.MapPost("/subscriptions/{id:guid}/seats", AssignSeatAsync)
            .WithName("AssignSeat")
            .WithSummary("Assigns a seat to a user.")
            .WithDescription("Adds a user to the subscription. Fails if the seat limit is reached.")
            .WithMetadata(new IdempotentAttribute())
            .Produces<SeatResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(SubscriptionsPermissions.Seats.Manage)
            .AllowHostAccess();

        group.MapDelete("/subscriptions/{id:guid}/seats/{userId:guid}", RevokeSeatAsync)
            .WithName("RevokeSeat")
            .WithSummary("Revokes a seat from a user.")
            .WithDescription("Removes the user's seat assignment from the subscription.")
            .WithMetadata(new IdempotentAttribute())
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(SubscriptionsPermissions.Seats.Manage)
            .AllowHostAccess();

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<SeatResponse>>, ProblemHttpResult>> ListSeatsAsync(
        Guid id,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISeatReader seatReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null || sub.TenantId != currentTenant.Id!.Value)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        IReadOnlyList<SubscriptionSeat> seats = await seatReader
            .GetSeatsAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<SeatResponse>>(
            seats.Select(SeatResponse.FromEntity).ToList());
    }

    private static async Task<Results<Created<SeatResponse>, ProblemHttpResult>> AssignSeatAsync(
        Guid id,
        SeatAssignRequest request,
        [FromServices] ISubscriptionReader reader,
        [FromServices] ISeatWriter seatWriter,
        [FromServices] IPlanReader planReader,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null || sub.TenantId != currentTenant.Id!.Value)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(sub.PlanId.Value), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            SubscriptionSeat seat = await seatWriter.AssignSeatAsync(
                SubscriptionId.Create(id), request.UserId, plan?.SeatLimit, cancellationToken)
                .ConfigureAwait(false);

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
        [FromServices] ISeatWriter seatWriter,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.IsAvailable)
        {
            return TypedResults.Problem("Tenant context required.", statusCode: StatusCodes.Status400BadRequest);
        }

        Subscription? sub = await reader
            .GetByIdAsync(SubscriptionId.Create(id), cancellationToken).ConfigureAwait(false);

        if (sub is null || sub.TenantId != currentTenant.Id!.Value)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        bool revoked = await seatWriter.RevokeSeatAsync(
            SubscriptionId.Create(id), userId, cancellationToken).ConfigureAwait(false);

        return revoked
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }
}
