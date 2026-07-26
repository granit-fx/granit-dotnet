using System.Diagnostics;
using Granit.Events;
using Granit.Http.Idempotency;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.Settings.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
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
        [FromServices] UserManager<LocalIdentity> userManager,
        [FromServices] IEmailConfirmationService emailConfirmation,
        [FromServices] IDistributedEventBus eventBus,
        [FromServices] IdentityLocalMetrics metrics,
        [FromServices] IClock clock,
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

        LocalIdentity newUser = new()
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,

            // Stamp the registration-event intent in the same commit that inserts the user, so the
            // reconciler can recover the UserRegisteredEto if the inline publish below is lost to a
            // crash (the identity DbContext is not enrolled in the Wolverine outbox). This closes the
            // gap that the provider-agnostic CreateUserAsync path (shared with admin-create, which
            // must NOT emit the event) could not.
            RegistrationEventPendingSince = clock.Now,
        };

        IdentityResult createResult = await userManager
            .CreateAsync(newUser, request.Password).ConfigureAwait(false);

        if (!createResult.Succeeded)
        {
            // Duplicate username/email → silently succeed (202) to prevent email enumeration.
            // Classified on the stable Duplicate* error codes, not the localized message, so a host
            // wiring a translated IdentityErrorDescriber cannot regress this path.
            if (createResult.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return TypedResults.Accepted((string?)null);
            }

            return ProblemFactory.Localized(
                httpContext,
                "Granit:Identity:Account:RegistrationRejected",
                "The account could not be created. Please review your details and try again.",
                StatusCodes.Status422UnprocessableEntity);
        }

        await emailConfirmation.SendConfirmationEmailAsync(
            newUser.Id.ToString(), request.Email, cancellationToken).ConfigureAwait(false);

        // Fast path: publish inline, then clear the pending marker so the reconciler skips it. A crash
        // before the clear leaves the marker set and the sweep re-publishes (at-least-once); consumers
        // of UserRegisteredEto are idempotent.
        await eventBus.PublishAsync(
            new UserRegisteredEto(newUser.Id, newUser.TenantId), cancellationToken).ConfigureAwait(false);

        newUser.RegistrationEventPendingSince = null;
        await userManager.UpdateAsync(newUser).ConfigureAwait(false);

        metrics.RecordRegistration(newUser.TenantId?.ToString());

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
