using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Write endpoints for creating and updating users in the identity provider.
/// </summary>
internal static class IdentityProviderUserWriteEndpoints
{
    internal static RouteGroupBuilder MapProviderUserWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateUserAsync)
            .WithName("CreateIdentityProviderUser")
            .WithSummary("Creates a new user in the identity provider.")
            .WithDescription("Creates a user in the upstream identity provider (Keycloak, Cognito, etc.). Returns the created user with its provider-assigned ID. Returns 501 if the provider does not support user creation.")
            .Produces<IdentityUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPut("/{userId}", UpdateUserAsync)
            .WithName("UpdateIdentityProviderUser")
            .WithSummary("Updates an existing user in the identity provider.")
            .WithDescription("Updates the mutable fields (email, name, attributes) of a user in the identity provider. Only provided fields are updated.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPatch("/{userId}/enabled", SetUserEnabledAsync)
            .WithName("SetIdentityProviderUserEnabled")
            .WithSummary("Enables or disables a user in the identity provider.")
            .WithDescription("Toggles the enabled state of a user account. A disabled user cannot authenticate.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Results<Created<IdentityUserResponse>, ProblemHttpResult>> CreateUserAsync(
        IdentityUserCreateRequest request,
        HttpContext httpContext,
        [FromServices] IIdentityUserWriter userWriter,
        [FromServices] IIdentityProviderCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        if (!capabilities.SupportsUserCreation)
        {
            return TypedResults.Problem(
                detail: $"The '{capabilities.ProviderName}' provider does not support user creation.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        var model = new IdentityUserCreate(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Enabled,
            request.TemporaryPassword);

        IIdentityUser created = await userWriter
            .CreateUserAsync(model, cancellationToken)
            .ConfigureAwait(false);

        IdentityUserResponse response = IdentityResponseMapper.ToResponse(created);
        string basePath = httpContext.Request.Path.Value!.TrimEnd('/');
        return TypedResults.Created($"{basePath}/{response.UserId}", response);
    }

    private static async Task<NoContent> UpdateUserAsync(
        string userId,
        IdentityUserUpdateRequest request,
        [FromServices] IIdentityUserWriter userWriter,
        CancellationToken cancellationToken)
    {
        var model = new IdentityUserUpdate(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Attributes);

        await userWriter.UpdateUserAsync(userId, model, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SetUserEnabledAsync(
        string userId,
        IdentityUserSetEnabledRequest request,
        [FromServices] IIdentityUserWriter userWriter,
        CancellationToken cancellationToken)
    {
        await userWriter.SetUserEnabledAsync(userId, request.Enabled, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
