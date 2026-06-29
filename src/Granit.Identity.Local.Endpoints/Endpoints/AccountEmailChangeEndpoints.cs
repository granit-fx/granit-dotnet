using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AccountEmailChangeEndpoints
{
    internal static RouteGroupBuilder MapAccountEmailChangeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/change-email", ChangeEmailAsync)
            .WithName("ChangeEmail")
            .WithSummary("Requests an email address change.")
            .WithDescription(
                "Requires password confirmation as step-up authentication (OWASP ASVS V2.8.1) "
                + "so a stolen session cookie alone cannot pivot a compromised account into a "
                + "permanent take-over via email change followed by password reset. "
                + "Generates a change-email token and publishes an EmailChangeRequestedEto event. "
                + "A security alert is sent to the current email and a confirmation link to the new email. "
                + "Returns 400 if the current password is incorrect; otherwise always returns 202 "
                + "to prevent email enumeration.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization();

        group.MapPost("/confirm-email-change", ConfirmEmailChangeAsync)
            .WithName("ConfirmEmailChange")
            .WithSummary("Confirms an email address change using a token.")
            .WithDescription(
                "Validates the email change token from the confirmation link, applies the new email, "
                + "and updates the username to match. Returns 400 if the token is invalid or expired.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .AllowAnonymous();

        return group;
    }

    private static async Task<Results<Accepted<string>, ProblemHttpResult>> ChangeEmailAsync(
        AccountChangeEmailRequest request,
        HttpContext httpContext,
        [FromServices] IIdentityCredentialVerifier credentialVerifier,
        [FromServices] IEmailChangeService emailChangeService,
        CancellationToken cancellationToken)
    {
        string? userId = httpContext.User.FindFirst("sub")?.Value;
        string? username = httpContext.User.FindFirst("preferred_username")?.Value
                           ?? httpContext.User.FindFirst("name")?.Value;

        if (userId is null || username is null)
        {
            // Token does not carry the claims we need to verify credentials. Return
            // 202 so a malformed token cannot be used to enumerate the endpoint.
            return TypedResults.Accepted((string?)null, (string?)null);
        }

        // Step-up: verify the current password before proceeding (OWASP ASVS V2.8.1).
        bool isValid = await credentialVerifier
            .VerifyUserCredentialsAsync(username, request.CurrentPassword, cancellationToken)
            .ConfigureAwait(false);

        if (!isValid)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:CurrentPasswordIncorrect", "Current password is incorrect."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        await emailChangeService.RequestChangeAsync(userId, request.NewEmail, cancellationToken)
            .ConfigureAwait(false);

        string? tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
        httpContext.RequestServices.GetService<IdentityLocalMetrics>()?.RecordEmailChangeRequest(tenantId);

        // Always return 202 on success to prevent email-existence enumeration.
        return TypedResults.Accepted((string?)null, (string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailChangeAsync(
        AccountConfirmEmailChangeRequest request,
        HttpContext httpContext,
        [FromServices] IEmailChangeService emailChangeService,
        CancellationToken cancellationToken)
    {
        bool confirmed = await emailChangeService
            .ConfirmChangeAsync(request.UserId, request.NewEmail, request.Token, cancellationToken)
            .ConfigureAwait(false);

        if (!confirmed)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:InvalidEmailChangeToken", "Invalid or expired email change token."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.NoContent();
    }
}
