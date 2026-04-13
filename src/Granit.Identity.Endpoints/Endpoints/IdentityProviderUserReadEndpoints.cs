using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Read endpoints for querying users directly from the identity provider.
/// </summary>
internal static class IdentityProviderUserReadEndpoints
{
    internal static RouteGroupBuilder MapProviderUserReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUsersAsync)
            .WithName("GetIdentityProviderUsers")
            .WithSummary("Lists users from the identity provider with optional search and pagination.")
            .WithDescription("Queries the identity provider directly (Keycloak, Cognito, etc.) for user records. Supports free-text search and pagination. Unlike the cache endpoints, this always hits the provider.")
            .Produces<IReadOnlyList<IdentityUserResponse>>();

        group.MapGet("/{userId}", GetUserAsync)
            .WithName("GetIdentityProviderUser")
            .WithSummary("Gets a single user by ID from the identity provider.")
            .WithDescription("Fetches a user record directly from the identity provider. Returns 404 if the user does not exist in the provider.")
            .Produces<IdentityUserResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentityUserResponse>>> GetUsersAsync(
        [FromServices] IIdentityUserReader userReader,
        [FromQuery] string? search,
        [FromQuery] int? first,
        [FromQuery] int? max,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IIdentityUser> users = await userReader
            .GetUsersAsync(search, first, max, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<IdentityUserResponse>>(
            users.Select(IdentityResponseMapper.ToResponse).ToList());
    }

    private static async Task<Results<Ok<IdentityUserResponse>, ProblemHttpResult>> GetUserAsync(
        string userId,
        [FromServices] IIdentityUserReader userReader,
        CancellationToken cancellationToken)
    {
        IIdentityUser? user = await userReader.GetUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(IdentityResponseMapper.ToResponse(user));
    }
}
