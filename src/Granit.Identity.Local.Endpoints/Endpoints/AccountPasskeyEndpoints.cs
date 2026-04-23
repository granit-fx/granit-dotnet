using Granit.Http.Idempotency.Attributes;
using Granit.Http.SecurityHeaders.Extensions;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Internal;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AccountPasskeyEndpoints
{
    internal static RouteGroupBuilder MapAccountPasskeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/passkeys", ListPasskeysAsync)
            .WithName("ListPasskeys")
            .WithSummary("Lists registered passkeys.")
            .WithDescription("Returns the list of WebAuthn passkeys registered for the authenticated user.")
            .Produces<IReadOnlyList<PasskeyInfoResponse>>()
            .RequireAuthorization()
            .WithNoStoreResponse();

        group.MapPost("/passkeys/register/begin", BeginRegistrationAsync)
            .WithName("BeginPasskeyRegistration")
            .WithSummary("Begins a WebAuthn passkey registration ceremony.")
            .WithDescription(
                "Returns PublicKeyCredentialCreationOptions with mediation: conditional "
                + "for browser-native passkey autofill support.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .RequireAuthorization()
            .WithNoStoreResponse();

        group.MapPost("/passkeys/register/complete", CompleteRegistrationAsync)
            .WithName("CompletePasskeyRegistration")
            .WithSummary("Completes a WebAuthn passkey registration ceremony.")
            .WithDescription("Validates and stores the credential via ASP.NET Identity's built-in WebAuthn support.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<PasskeyInfoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        group.MapPost("/passkeys/assertion/begin", BeginAssertionAsync)
            .WithName("BeginPasskeyAssertion")
            .WithSummary("Begins a WebAuthn passkey assertion ceremony (login).")
            .WithDescription(
                "Returns PublicKeyCredentialRequestOptions with mediation: conditional "
                + "and empty allowCredentials for Conditional UI autofill.")
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .AllowAnonymous()
            .RequireRateLimiting("authentication")
            .WithNoStoreResponse();

        group.MapPost("/passkeys/assertion/complete", CompleteAssertionAsync)
            .WithName("CompletePasskeyAssertion")
            .WithSummary("Completes a WebAuthn passkey assertion ceremony (login).")
            .WithDescription(
                "Validates the AuthenticatorAssertionResponse from the browser against "
                + "stored public keys. On success, signs in the user via ASP.NET Core Identity "
                + "and returns the login response. Returns 401 if the credential is invalid "
                + "or no matching user is found.")
            .Produces<AccountLoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .AllowAnonymous()
            .RequireRateLimiting("authentication");

        group.MapPatch("/passkeys/{id:guid}", RenamePasskeyAsync)
            .WithName("RenamePasskey")
            .WithSummary("Renames a passkey.")
            .WithDescription("Updates the friendly name of a registered passkey.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapDelete("/passkeys/{id:guid}", DeletePasskeyAsync)
            .WithName("DeletePasskey")
            .WithSummary("Deletes a passkey.")
            .WithDescription(
                "Removes a registered passkey. Returns 400 if this is the last credential "
                + "and no password is set on the account.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<PasskeyInfoResponse>>> ListPasskeysAsync(
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        IReadOnlyList<PasskeyInfo> passkeys = await passkeyService
            .GetPasskeysAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok<IReadOnlyList<PasskeyInfoResponse>>(
            passkeys.Select(IdentityLocalResponseMapper.ToResponse).ToList());
    }

    private static async Task<Ok<string>> BeginRegistrationAsync(
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        string optionsJson = await passkeyService
            .BeginRegistrationAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(optionsJson);
    }

    private static async Task<Results<Created<PasskeyInfoResponse>, ProblemHttpResult>> CompleteRegistrationAsync(
        PasskeyRegistrationRequest request,
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        try
        {
            PasskeyInfo passkey = await passkeyService
                .CompleteRegistrationAsync(userId, request.CredentialJson, request.Name, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Created(
                $"/api/account/passkeys/{passkey.Id}",
                IdentityLocalResponseMapper.ToResponse(passkey));
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Ok<string>> BeginAssertionAsync(
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string optionsJson = await passkeyService
            .BeginAssertionAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(optionsJson);
    }

    private static async Task<Results<Ok<AccountLoginResponse>, ProblemHttpResult>> CompleteAssertionAsync(
        AccountPasskeyLoginRequest request,
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        [FromServices] SignInManager<GranitUser> signInManager,
        [FromServices] UserManager<GranitUser> userManager,
        CancellationToken cancellationToken)
    {
        IdentityLocalMetrics? metrics = httpContext.RequestServices.GetService<IdentityLocalMetrics>();

        GranitPasskeyAssertionResult assertion = await passkeyService
            .CompleteAssertionAsync(request.CredentialJson, cancellationToken)
            .ConfigureAwait(false);

        if (!assertion.Succeeded || assertion.UserId is null)
        {
            metrics?.RecordAuthenticationFailure(null, "invalid_token");

            return TypedResults.Problem(
                detail: "Passkey authentication failed.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        GranitUser? user = await userManager.FindByIdAsync(assertion.UserId).ConfigureAwait(false);

        if (user is null)
        {
            return TypedResults.Problem(
                detail: "User not found.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        await signInManager.SignInAsync(user, isPersistent: false).ConfigureAwait(false);
        metrics?.RecordAuthenticationSuccess(null, "passkey");

        return TypedResults.Ok(new AccountLoginResponse(Succeeded: true));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RenamePasskeyAsync(
        Guid id,
        PasskeyRenameRequest request,
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        await passkeyService.RenameAsync(userId, id, request.Name, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeletePasskeyAsync(
        Guid id,
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        try
        {
            await passkeyService.DeleteAsync(userId, id, cancellationToken).ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
