using Granit.Core.Events;
using Granit.Identity;
using Granit.Identity.Models;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Events;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountRegistrationEndpoints
{
    internal static RouteGroupBuilder MapAccountRegistrationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterAsync)
            .WithName("RegisterAccount")
            .WithSummary("Registers a new user account.")
            .WithDescription(
                "Creates a new user with the provided email and password. "
                + "Sends a confirmation email if email confirmation is required. "
                + "Returns 409 if the email is already taken. "
                + "Returns 422 if the password does not meet policy requirements.")
            .Produces<AccountRegisterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesValidationProblem()
            .AllowAnonymous();

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

    private static async Task<Results<Created<AccountRegisterResponse>, ProblemHttpResult>> RegisterAsync(
        AccountRegisterRequest request,
[FromServices] IIdentityProvider identityProvider,
[FromServices] IEmailConfirmationService emailConfirmation,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] OpenIddictMetrics metrics,
        CancellationToken cancellationToken)
    {
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
                new UserRegisteredEto(Guid.Parse(user.UserId), request.Email, null),
                cancellationToken).ConfigureAwait(false);

            metrics.RecordRegistration(null);

            return TypedResults.Created(
                $"/api/account/profile",
                new AccountRegisterResponse(Guid.Parse(user.UserId), true));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already taken", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: "An account with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmEmailAsync(
        string userId,
        string token,
        [FromServices] IEmailConfirmationService emailConfirmation,
        CancellationToken cancellationToken)
    {
        bool confirmed = await emailConfirmation.ConfirmAsync(userId, token, cancellationToken)
            .ConfigureAwait(false);

        if (!confirmed)
        {
            return TypedResults.Problem(
                detail: "Invalid or expired confirmation token.",
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
