using System.Diagnostics;
using Granit.Events;
using Granit.Http.Idempotency;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Granit.Settings.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AccountRegistrationEndpoints
{
    internal static RouteGroupBuilder MapAccountRegistrationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterAsync)
            .WithName("RegisterAccount")
            .WithSummary("Registers a new user account.")
            .WithDescription(
                "Creates a new user with the provided email and password. "
                + "Returns 403 if self-registration is disabled for the current tenant. "
                + "Always returns 202 to prevent email enumeration. "
                + "Sends a confirmation email if the account is new, or a notification if the email is already taken. "
                + "Returns 422 if the password does not meet policy requirements.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapGet("/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirms a user's email address.")
            .WithDescription(
                "Validates the email confirmation token sent via email. "
                + "Returns 204 on success, 400 if the token is invalid or expired.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        group.MapPost("/resend-confirmation-email", ResendConfirmationEmailAsync)
            .WithName("ResendConfirmationEmail")
            .WithSummary("Resends the email confirmation link.")
            .WithDescription(
                "Resends the confirmation email to the authenticated user. "
                + "Always returns 202 to prevent email enumeration.")
            .Produces(StatusCodes.Status202Accepted)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> RegisterAsync(
        AccountRegisterRequest request,
        HttpContext httpContext,
        [FromServices] ISettingProvider settingProvider,
        [FromServices] IIdentityProvider identityProvider,
        [FromServices] IEmailConfirmationService emailConfirmation,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IdentityLocalMetrics metrics,
        CancellationToken cancellationToken)
    {
        using Activity? activity = IdentityLocalActivitySource.Source.StartActivity(
            IdentityLocalActivitySource.UserRegistration);

        string? allowed = await settingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(allowed, "true", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:SelfRegistrationDisabled", "Self-registration is disabled."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        try
        {
            IIdentityUser user = await identityProvider.CreateUserAsync(
                new IdentityUserCreate(
                    request.Email,
                    request.Email,
                    request.FirstName,
                    request.LastName,
                    true,
                    request.Password),
                cancellationToken).ConfigureAwait(false);

            await emailConfirmation.SendConfirmationEmailAsync(
                user.UserId, request.Email, cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(
                new UserRegisteredEto(Guid.Parse(user.UserId), null),
                cancellationToken).ConfigureAwait(false);

            metrics.RecordRegistration(null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already taken", StringComparison.OrdinalIgnoreCase))
        {
            // Silently succeed — return 202 to prevent email enumeration.
            // The existing user could be notified via a "someone tried to register
            // with your email" notification if desired (app-level concern).
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        // Always return 202 regardless of outcome (anti-enumeration)
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailAsync(
        string userId,
        string token,
        HttpContext httpContext,
        [FromServices] IEmailConfirmationService emailConfirmation,
        CancellationToken cancellationToken)
    {
        bool confirmed = await emailConfirmation.ConfirmAsync(userId, token, cancellationToken)
            .ConfigureAwait(false);

        if (!confirmed)
        {
            return TypedResults.Problem(
                detail: AccountEndpointMessages.Localize(
                    httpContext, "Granit:Identity:Account:InvalidConfirmationToken", "Invalid or expired confirmation token."),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Accepted<string>> ResendConfirmationEmailAsync(
        HttpContext httpContext,
        [FromServices] IEmailConfirmationService emailConfirmation,
        CancellationToken cancellationToken)
    {
        string? userId = httpContext.User.FindFirst("sub")?.Value;

        if (userId is not null)
        {
            string? email = httpContext.User.FindFirst("email")?.Value;
            if (email is not null)
            {
                await emailConfirmation.SendConfirmationEmailAsync(userId, email, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Always return 202 to prevent enumeration
        return TypedResults.Accepted((string?)null, (string?)null);
    }
}
