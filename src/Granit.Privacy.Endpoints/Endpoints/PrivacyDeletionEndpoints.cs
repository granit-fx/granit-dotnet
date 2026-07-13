using Granit.Privacy.DataDeletion;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.Regulations;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Privacy.Endpoints.Endpoints;

internal static class PrivacyDeletionEndpoints
{
    internal static RouteGroupBuilder MapPrivacyDeletionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/deletions", HandleRequestDeletionAsync)
             .RequireAuthorization(PrivacyPermissions.Deletions.Execute)
             .WithName("RequestPrivacyDeletion")
             .WithSummary("Requests personal data deletion for the current user.")
             .WithDescription(
                 "Publishes a distributed deletion event (GDPR Art. 17 — right to erasure). "
                 + "When Defer is false, deletion is immediate. When Defer is true, a cooling-off "
                 + "period starts — the user receives a reminder email before the "
                 + "deadline and can cancel via POST /deletion/{requestId}/cancel. "
                 + "A confirmation email is sent in both cases after deletion is executed. "
                 + "Do not include personally identifiable information in the Reason field.")
             .Produces<PrivacyDeletionRequestResponse>(StatusCodes.Status202Accepted)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/deletions/{requestId:guid}/cancel", HandleCancelDeletionAsync)
             .RequireAuthorization(PrivacyPermissions.Deletions.Execute)
             .WithName("CancelPrivacyDeletion")
             .WithSummary("Cancels a deferred deletion request during the grace period.")
             .WithDescription(
                 "Cancels a deferred deletion request. Only requests in Deferred state can be "
                 + "cancelled. Returns 404 if the request is not found, 409 if already executed or cancelled.")
             .Produces(StatusCodes.Status200OK)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/deletions/{requestId:guid}", HandleGetDeletionStatusAsync)
             .WithName("GetPrivacyDeletionStatus")
             .WithSummary("Returns the status of a deferred deletion request.")
             .WithDescription(
                 "Queries the deletion request tracker for the specified request ID. "
                 + "Only the user who created the request can view its status. "
                 + "Returns the current state (Deferred, Executed, Cancelled), scheduled deletion date, "
                 + "and timestamps. Returns 404 if the request is not found or belongs to another user.")
             .Produces<PrivacyDeletionStatusResponse>()
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/deletions", HandleGetMyDeletionsAsync)
             .WithName("ListPrivacyDeletions")
             .WithSummary("Lists all deletion requests for the current user.")
             .WithDescription(
                 "Returns all deferred deletion requests submitted by the current user, "
                 + "ordered by most recent first. Immediate deletions are not tracked.")
             .Produces<IReadOnlyList<PrivacyDeletionStatusResponse>>()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Accepted<PrivacyDeletionRequestResponse>, Accepted, ProblemHttpResult>> HandleRequestDeletionAsync(
        PrivacyDeletionRequest body,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IPrivacyDeletionRequestService deletionService,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        string regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);
        string requestedBy = currentUser.Email ?? "unknown";

        RequestDeletionOutcome outcome = await deletionService
            .RequestDeletionAsync(
                new RequestDeletionCommand(userId, requestedBy, body.Defer, body.Reason, regulation),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome.Result switch
        {
            RequestDeletionResult.DuplicatePending => TypedResults.Problem(
                detail: "A deferred deletion request is already in progress. Cancel it before submitting a new one.",
                statusCode: StatusCodes.Status409Conflict),
            RequestDeletionResult.Deferred => TypedResults.Accepted(
                $"/privacy/deletion/{outcome.RequestId}",
                new PrivacyDeletionRequestResponse(outcome.RequestId, outcome.ScheduledDeletionAt!.Value)),
            _ => TypedResults.Accepted((string?)null),
        };
    }

    private static async Task<Results<Ok, ProblemHttpResult>> HandleCancelDeletionAsync(
        Guid requestId,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IPrivacyDeletionRequestService deletionService,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        CancelDeletionOutcome outcome = await deletionService
            .CancelDeletionAsync(requestId, userId, cancellationToken)
            .ConfigureAwait(false);

        return outcome.Result switch
        {
            CancelDeletionResult.NotFound => TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound),
            CancelDeletionResult.NotCancellable => TypedResults.Problem(
                detail: $"Deletion request '{requestId}' is already {outcome.CurrentState} and cannot be cancelled.",
                statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Ok(),
        };
    }

    private static async Task<Results<Ok<PrivacyDeletionStatusResponse>, ProblemHttpResult>> HandleGetDeletionStatusAsync(
        Guid requestId,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDeletionRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        DeletionRequestStatus? status = await tracker
            .GetStatusAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (status is null || status.UserId != userId)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(PrivacyResponseMapper.MapDeletionStatus(status));
    }

    private static async Task<Results<Ok<IReadOnlyList<PrivacyDeletionStatusResponse>>, ProblemHttpResult>> HandleGetMyDeletionsAsync(
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDeletionRequestTrackerReader tracker,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        IReadOnlyList<DeletionRequestStatus> statuses = await tracker
            .GetByUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PrivacyDeletionStatusResponse> result = statuses
            .Select(PrivacyResponseMapper.MapDeletionStatus)
            .ToList();

        return TypedResults.Ok(result);
    }
}
