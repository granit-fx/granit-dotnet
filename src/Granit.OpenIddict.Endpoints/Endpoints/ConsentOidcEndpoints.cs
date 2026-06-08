using Granit.OpenIddict.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class ConsentOidcEndpoints
{
    internal static RouteGroupBuilder MapConsentOidcEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder apps = group.MapGroup("/applications");

        apps.MapGet("/{clientId}", GetApplicationInfoAsync)
            .WithName("GetOidcApplicationInfo")
            .WithSummary("Returns public display info for an OIDC application.")
            .WithDescription("Returns the client ID and display name of an OIDC application. Used by the consent page to identify which application is requesting access. Requires authentication but no admin permission. Returns 404 if no application exists for the given client ID.")
            .Produces<OidcApplicationInfoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<OidcApplicationInfoResponse>, ProblemHttpResult>> GetApplicationInfoAsync(
        string clientId,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        object? app = await applicationManager.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        string? displayName = await applicationManager.GetDisplayNameAsync(app, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new OidcApplicationInfoResponse(clientId, displayName));
    }
}
