using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Endpoints.Internal;
using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.Options;
using Granit.Privacy.Regulations;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

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
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IDeletionRequestTrackerReader deletionTracker,
        [FromServices] IDeletionRequestTrackerWriter? deletionTrackerWriter,
        [FromServices] PrivacyMetrics metrics,
        [FromServices] TimeProvider timeProvider,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] IOptions<GranitPrivacyOptions> privacyOptions,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] IPrivacyRegulationResolver? regulationResolver,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        IReadOnlyList<DeletionRequestStatus> existing = await deletionTracker
            .GetByUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (existing.Any(r => r.State == DeletionRequestState.Deferred))
        {
            return TypedResults.Problem(
                detail: "A deferred deletion request is already in progress. Cancel it before submitting a new one.",
                statusCode: StatusCodes.Status409Conflict);
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string requestedBy = currentUser.Email ?? "unknown";
        string regulation = await PrivacyResponseMapper.ResolveRegulationAsync(regulationResolver, cancellationToken).ConfigureAwait(false);

        if (body.Defer)
        {
            int graceDays = privacyOptions.Value.DefaultGracePeriodDays;
            DateTimeOffset scheduledDeletionAt = now.AddDays(graceDays);

            await eventBus
                .PublishAsync(
                    new DeletionDeferredEto(requestId, userId, requestedBy, now, body.Reason, scheduledDeletionAt, regulation, tenantId),
                    cancellationToken)
                .ConfigureAwait(false);

            metrics.RecordDeletionDeferred(tenantId, regulation);

            return TypedResults.Accepted(
                $"/privacy/deletion/{requestId}",
                new PrivacyDeletionRequestResponse(requestId, scheduledDeletionAt));
        }

        // Immediate deletion + confirmation event + audit trail (GDPR Art. 5(2))
        if (deletionTrackerWriter is not null)
        {
            await deletionTrackerWriter
                .RecordImmediateDeletionAsync(requestId, userId, body.Reason, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await eventBus
            .PublishAsync(
                new PersonalDataDeletionRequestedEto(requestId, userId, requestedBy, now, body.Reason, regulation, tenantId),
                cancellationToken)
            .ConfigureAwait(false);

        await eventBus
            .PublishAsync(
                new DeletionExecutedEto(requestId, userId, now),
                cancellationToken)
            .ConfigureAwait(false);

        metrics.RecordDeletionRequested(tenantId, regulation);
        metrics.RecordDeletionExecuted(tenantId, regulation);

        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<Ok, ProblemHttpResult>> HandleCancelDeletionAsync(
        Guid requestId,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IDeletionRequestTrackerReader tracker,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!PrivacyResponseMapper.TryGetUserId(currentUser, out Guid userId))
        {
            return PrivacyResponseMapper.UserNotAuthenticated();
        }

        DeletionRequestStatus? status = await tracker
            .GetStatusAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (status is null)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (status.UserId != userId)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (status.State != DeletionRequestState.Deferred)
        {
            return TypedResults.Problem(
                detail: $"Deletion request '{requestId}' is already {status.State} and cannot be cancelled.",
                statusCode: StatusCodes.Status409Conflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        await eventBus
            .PublishAsync(
                new DeletionCancelledEto(requestId, userId, now),
                cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok();
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
