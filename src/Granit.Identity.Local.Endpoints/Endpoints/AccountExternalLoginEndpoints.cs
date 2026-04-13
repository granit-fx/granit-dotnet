using Granit.Http.Idempotency.Attributes;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Endpoints;

internal static class AccountExternalLoginEndpoints
{
    internal static RouteGroupBuilder MapAccountExternalLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/external-logins", ListExternalLoginsAsync)
            .WithName("ListExternalLogins")
            .WithSummary("Lists linked external login providers.")
            .WithDescription(
                "Returns the list of external providers (Google, Microsoft, GitHub) "
                + "currently linked to the authenticated user's account.")
            .Produces<IReadOnlyList<ExternalLoginInfo>>()
            .RequireAuthorization();

        group.MapPost("/external-logins/challenge/{provider}", ChallengeAsync)
            .WithName("ChallengeExternalLogin")
            .WithSummary("Initiates an OAuth flow with an external provider.")
            .WithDescription(
                "Validates that the specified provider is registered in IExternalProviderRegistry "
                + "and returns 200 with metadata for the frontend to initiate the redirect "
                + "via the standard OAuth client flow. "
                + "Returns 400 if the provider is not configured.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        group.MapGet("/external-logins/callback", CallbackAsync)
            .WithName("ExternalLoginCallback")
            .WithSummary("Processes the OAuth callback from an external provider.")
            .WithDescription(
                "Handles the redirect from the external provider. Links the external account "
                + "to an existing user or creates a new one if AutoRegisterExternalUsers is enabled. "
                + "Returns 400 if the provider query parameter is missing. "
                + "Returns 409 if the email is taken by another account. "
                + "Returns 403 if auto-registration is disabled and no account exists.")
            .Produces<ProcessCallbackResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AllowAnonymous();

        group.MapDelete("/external-logins/{provider}", UnlinkExternalLoginAsync)
            .WithName("UnlinkExternalLogin")
            .WithSummary("Unlinks an external login provider.")
            .WithDescription(
                "Removes the association between the authenticated user and the specified provider. "
                + "Returns 400 if it's the last login method and no password is set.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<ExternalLoginInfo>>> ListExternalLoginsAsync(
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;
        IReadOnlyList<ExternalLoginInfo> logins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(logins);
    }

    private static Task<Results<Ok, ProblemHttpResult>> ChallengeAsync(
        string provider,
        [FromServices] IExternalProviderRegistry providerRegistry)
    {
        // Validate provider is configured
        bool isConfigured = providerRegistry.IsProviderConfigured(provider);

        if (!isConfigured)
        {
            return Task.FromResult<Results<Ok, ProblemHttpResult>>(
                TypedResults.Problem(
                    detail: "The specified external login provider is not configured.",
                    statusCode: StatusCodes.Status400BadRequest));
        }

        // The actual OAuth challenge is initiated by the auth server's client middleware.
        // The host application configures challenge properties and calls ChallengeAsync()
        // on the authentication scheme corresponding to the provider.
        // This endpoint validates the provider and returns metadata for the frontend
        // to initiate the redirect via the standard OAuth client flow.
        return Task.FromResult<Results<Ok, ProblemHttpResult>>(TypedResults.Ok());
    }

    private static async Task<Results<Ok<ProcessCallbackResult>, ProblemHttpResult>> CallbackAsync(
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        // The auth server's client middleware populates HttpContext.User with the external
        // provider's claims after a successful OAuth callback. The provider name is
        // available from the authentication scheme or a query parameter.
        string provider = httpContext.Request.Query["provider"].ToString();

        if (string.IsNullOrEmpty(provider))
        {
            return TypedResults.Problem(
                detail: "Missing 'provider' query parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            ProcessCallbackResult result = await externalLoginService
                .ProcessCallbackAsync(httpContext.User, provider, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DuplicateEmail", StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.Problem(
                detail: "An account with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UnlinkExternalLoginAsync(
        string provider,
        HttpContext httpContext,
        [FromServices] IExternalLoginService externalLoginService,
        CancellationToken cancellationToken)
    {
        string userId = httpContext.User.FindFirst("sub")!.Value;

        // Get the provider key for this provider
        IReadOnlyList<ExternalLoginInfo> logins = await externalLoginService
            .GetLoginsAsync(userId, cancellationToken).ConfigureAwait(false);

        ExternalLoginInfo? login = logins.FirstOrDefault(l =>
            l.LoginProvider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (login is null)
        {
            return TypedResults.Problem(
                detail: $"No linked login found for provider '{provider}'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await externalLoginService
                .RemoveLoginAsync(userId, login.LoginProvider, login.ProviderKey, cancellationToken)
                .ConfigureAwait(false);
            return TypedResults.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
