using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AccountPasskeyEndpoints
{
    internal static RouteGroupBuilder MapAccountPasskeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/passkeys", ListPasskeysAsync)
            .WithName("ListPasskeys")
            .WithSummary("Lists registered passkeys.")
            .WithDescription("Returns the list of WebAuthn passkeys registered for the authenticated user.")
            .Produces<IReadOnlyList<PasskeyInfo>>()
            .RequireAuthorization();

        group.MapPost("/passkeys/register/begin", BeginRegistrationAsync)
            .WithName("BeginPasskeyRegistration")
            .WithSummary("Begins a WebAuthn passkey registration ceremony.")
            .WithDescription(
                "Returns PublicKeyCredentialCreationOptions with mediation: conditional "
                + "for browser-native passkey autofill support.")
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .RequireAuthorization();

        group.MapPost("/passkeys/register/complete", CompleteRegistrationAsync)
            .WithName("CompletePasskeyRegistration")
            .WithSummary("Completes a WebAuthn passkey registration ceremony.")
            .WithDescription("Validates and stores the credential via ASP.NET Identity's built-in WebAuthn support.")
            .Produces<PasskeyInfo>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        group.MapPost("/passkeys/assertion/begin", BeginAssertionAsync)
            .WithName("BeginPasskeyAssertion")
            .WithSummary("Begins a WebAuthn passkey assertion ceremony (login).")
            .WithDescription(
                "Returns PublicKeyCredentialRequestOptions with mediation: conditional "
                + "and empty allowCredentials for Conditional UI autofill.")
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .AllowAnonymous();

        group.MapPatch("/passkeys/{id:guid}", RenamePasskeyAsync)
            .WithName("RenamePasskey")
            .WithSummary("Renames a passkey.")
            .WithDescription("Updates the friendly name of a registered passkey.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapDelete("/passkeys/{id:guid}", DeletePasskeyAsync)
            .WithName("DeletePasskey")
            .WithSummary("Deletes a passkey.")
            .WithDescription(
                "Removes a registered passkey. Returns 400 if this is the last credential "
                + "and no password is set on the account.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<PasskeyInfo>>> ListPasskeysAsync(
        HttpContext httpContext,
        [FromServices] IPasskeyService passkeyService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        IReadOnlyList<PasskeyInfo> passkeys = await passkeyService
            .GetPasskeysAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(passkeys);
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

    private static async Task<Results<Created<PasskeyInfo>, ProblemHttpResult>> CompleteRegistrationAsync(
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
            return TypedResults.Created($"/api/account/passkeys/{passkey.Id}", passkey);
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

    private static async Task<Results<NoContent, NotFound>> RenamePasskeyAsync(
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
