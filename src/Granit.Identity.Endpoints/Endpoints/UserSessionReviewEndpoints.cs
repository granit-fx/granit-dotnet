using Granit.Events;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Options;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// The anonymous, token-protected <c>/sessions/review</c> endpoints behind a "was this you?" alert email. GET is
/// side-effect-free (safe for email link scanners to prefetch); POST commits the decision and is single-use.
/// </summary>
internal static class UserSessionReviewEndpoints
{
    internal static RouteGroupBuilder MapUserSessionReviewEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAsync)
            .WithName("GetSessionReviewContext")
            .WithSummary("Returns the context for a session-review link.")
            .WithDescription(
                "Validates the signed review token and returns the flagged sign-in's context plus any decision "
                + "already recorded. Side-effect-free, so an email link scanner that prefetches the link cannot "
                + "trigger any action.")
            .Produces<SessionReviewContextResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/", PostAsync)
            .WithName("SubmitSessionReview")
            .WithSummary("Records a \"was this you?\" decision.")
            .WithDescription(
                "Commits the user's yes/no answer. \"Yes\" trusts the current device and reinforces the habitual "
                + "profile; \"No\" revokes all the user's sessions and triggers credential-reset remediation. "
                + "Single-use: a repeat submission (double-click, prefetch) is an idempotent no-op.")
            .Produces<SessionReviewResultResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

#pragma warning disable GRAPI003 // Private handlers — resolved via the route delegate, not exposed as a parameter
    private static async Task<Results<Ok<SessionReviewContextResponse>, ProblemHttpResult>> GetAsync(
        [FromQuery] string token,
        [FromServices] ISessionReviewTokenService tokenService,
        [FromServices] IUserSessionReviewStore reviewStore,
        CancellationToken cancellationToken)
    {
        SessionReviewTokenPayload? payload = tokenService.Validate(token);
        if (payload is null)
        {
            return InvalidToken();
        }

        // Read-only: report the country and whether a decision was already recorded. No state changes.
        UserSessionReviewDecision? decision = await reviewStore
            .GetDecisionAsync(payload.UserId, payload.SessionId, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new SessionReviewContextResponse(payload.Country, decision));
    }

    private static async Task<Results<Ok<SessionReviewResultResponse>, ProblemHttpResult>> PostAsync(
        SessionReviewDecisionRequest request,
        HttpContext httpContext,
        [FromServices] ISessionReviewTokenService tokenService,
        [FromServices] IUserSessionReviewStore reviewStore,
        [FromServices] IUserSessionManager sessionManager,
        [FromServices] IDeviceTrustStore deviceTrustStore,
        [FromServices] IUserBehavioralProfileStore profileStore,
        [FromServices] IOptions<DeviceTrustOptions> deviceTrustOptions,
        [FromServices] ICurrentTenant currentTenant,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        SessionReviewTokenPayload? payload = tokenService.Validate(request.Token);
        if (payload is null)
        {
            return InvalidToken();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        // Single-use gate: only the first call performs side effects. A repeat (double-click, scanner prefetch
        // of a POST) returns the authoritative recorded decision without re-revoking or re-publishing.
        bool applied = await reviewStore
            .TryRecordDecisionAsync(payload.UserId, payload.SessionId, request.Decision, now, cancellationToken)
            .ConfigureAwait(false);
        if (!applied)
        {
            UserSessionReviewDecision recorded = await reviewStore
                .GetDecisionAsync(payload.UserId, payload.SessionId, cancellationToken).ConfigureAwait(false)
                ?? request.Decision;
            return TypedResults.Ok(new SessionReviewResultResponse(recorded, Applied: false));
        }

        if (request.Decision == UserSessionReviewDecision.Confirmed)
        {
            if (payload.DeviceId is { } deviceId)
            {
                await deviceTrustStore.SetAsync(
                    payload.UserId,
                    deviceId,
                    new DeviceTrustVerdict(
                        DeviceTrustLevel.Remembered, now, now + deviceTrustOptions.Value.TrustDuration, "user_confirmed"),
                    cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(payload.Country))
            {
                await profileStore
                    .RecordObservationAsync(payload.UserId, payload.Country, null, null, now, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await sessionManager.RevokeAllAsync(payload.UserId, cancellationToken).ConfigureAwait(false);

            IDistributedEventBus? eventBus = httpContext.RequestServices.GetService<IDistributedEventBus>();
            if (eventBus is not null)
            {
                Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
                await eventBus
                    .PublishAsync(new SessionDeniedEto(payload.UserId, payload.SessionId, tenantId, now), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return TypedResults.Ok(new SessionReviewResultResponse(request.Decision, Applied: true));
    }

    private static ProblemHttpResult InvalidToken() =>
        TypedResults.Problem(
            detail: "The review link is invalid or has expired.",
            statusCode: StatusCodes.Status400BadRequest);
#pragma warning restore GRAPI003
}
