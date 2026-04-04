using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Endpoints.Dtos;
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
                "Generates a change-email token and publishes an EmailChangeRequestedEto event. "
                + "A security alert is sent to the current email and a confirmation link to the new email. "
                + "Always returns 202 to prevent email enumeration.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .RequireAuthorization();

        group.MapPost("/confirm-email-change", ConfirmEmailChangeAsync)
            .WithName("ConfirmEmailChange")
            .WithSummary("Confirms an email address change using a token.")
            .WithDescription(
                "Validates the email change token from the confirmation link, applies the new email, "
                + "and updates the username to match. Returns 400 if the token is invalid or expired.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem()
            .AllowAnonymous();

        return group;
    }

    private static async Task<Accepted<string>> ChangeEmailAsync(
        AccountChangeEmailRequest request,
        HttpContext httpContext,
        [FromServices] IEmailChangeService emailChangeService,
        CancellationToken cancellationToken)
    {
        string? userId = httpContext.User.FindFirst("sub")?.Value;

        if (userId is not null)
        {
            await emailChangeService.RequestChangeAsync(userId, request.NewEmail, cancellationToken)
                .ConfigureAwait(false);

            string? tenantId = httpContext.User.FindFirst("tenant_id")?.Value;
            httpContext.RequestServices.GetService<IdentityLocalMetrics>()?.RecordEmailChangeRequest(tenantId);
        }

        // Always return 202 to prevent enumeration
        return TypedResults.Accepted((string?)null, (string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailChangeAsync(
        AccountConfirmEmailChangeRequest request,
        [FromServices] IEmailChangeService emailChangeService,
        CancellationToken cancellationToken)
    {
        bool confirmed = await emailChangeService
            .ConfirmChangeAsync(request.UserId, request.NewEmail, request.Token, cancellationToken)
            .ConfigureAwait(false);

        if (!confirmed)
        {
            return TypedResults.Problem(
                detail: "Invalid or expired email change token.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.NoContent();
    }
}
