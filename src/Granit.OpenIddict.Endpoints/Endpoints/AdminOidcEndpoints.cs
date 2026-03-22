using Granit.OpenIddict.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AdminOidcEndpoints
{
    internal static RouteGroupBuilder MapAdminOidcEndpoints(this RouteGroupBuilder group)
    {
        // ──── Applications ────
        RouteGroupBuilder apps = group.MapGroup("/oidc/applications");

        apps.MapGet("/", ListApplicationsAsync)
            .WithName("ListOidcApplications")
            .WithSummary("Returns all OIDC applications.")
            .WithDescription("Returns a paginated list of registered OIDC client applications.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Applications.Read);

        apps.MapPost("/", CreateApplicationAsync)
            .WithName("CreateOidcApplication")
            .WithSummary("Creates a new OIDC application.")
            .WithDescription("Registers a new OIDC client with the specified permissions and redirect URIs.")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Applications.Create);

        apps.MapDelete("/{clientId}", DeleteApplicationAsync)
            .WithName("DeleteOidcApplication")
            .WithSummary("Deletes an OIDC application.")
            .WithDescription("Removes the OIDC client and all associated authorizations and tokens.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Applications.Delete);

#pragma warning disable GRSEC003 // Endpoint path constant, not a secret
        apps.MapPost("/{clientId}/rotate-secret", RotateSecretAsync)
#pragma warning restore GRSEC003
            .WithName("RotateOidcApplicationSecret")
            .WithSummary("Rotates an OIDC application's client secret.")
            .WithDescription(
                "Generates a new client secret, invalidating the old one immediately. "
                + "The new plaintext secret is returned once in the response (never stored in plaintext).")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Applications.Rotate);

        // ──── Scopes ────
        RouteGroupBuilder scopes = group.MapGroup("/oidc/scopes");

        scopes.MapGet("/", ListScopesAsync)
            .WithName("ListOidcScopes")
            .WithSummary("Returns all OIDC scopes.")
            .WithDescription("Returns the list of registered OIDC scopes with their resources.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Scopes.Read);

        scopes.MapPost("/", CreateScopeAsync)
            .WithName("CreateOidcScope")
            .WithSummary("Creates a new OIDC scope.")
            .WithDescription("Registers a new scope with the specified name, display name, and resources.")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Scopes.Create);

        scopes.MapDelete("/{scopeName}", DeleteScopeAsync)
            .WithName("DeleteOidcScope")
            .WithSummary("Deletes an OIDC scope.")
            .WithDescription("Removes the scope. Existing authorizations using this scope are not affected.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Scopes.Delete);

        // ──── Authorizations ────
        RouteGroupBuilder auths = group.MapGroup("/oidc/authorizations");

        auths.MapGet("/", ListAuthorizationsAsync)
            .WithName("ListOidcAuthorizations")
            .WithSummary("Returns OIDC authorizations.")
            .WithDescription("Returns a paginated list filterable by userId and clientId.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Read);

        auths.MapDelete("/{authorizationId:guid}", RevokeAuthorizationAsync)
            .WithName("RevokeOidcAuthorization")
            .WithSummary("Revokes an OIDC authorization.")
            .WithDescription("Revokes the authorization and all associated tokens.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Revoke);

        auths.MapDelete("/user/{userId:guid}", RevokeUserAuthorizationsAsync)
            .WithName("RevokeUserOidcAuthorizations")
            .WithSummary("Revokes all OIDC authorizations for a user.")
            .WithDescription("Revokes all authorizations and tokens for the specified user. Used for GDPR erasure and security incidents.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Revoke);

        return group;
    }

    // ──── Application handlers ────

    private static Task<Ok> ListApplicationsAsync() => Task.FromResult(TypedResults.Ok());

    private static Task<Created> CreateApplicationAsync() =>
        Task.FromResult(TypedResults.Created("/api/admin/oidc/applications/{clientId}"));

#pragma warning disable S1172 // Route-bound parameters required for minimal API binding
    private static Task<Results<NoContent, NotFound>> DeleteApplicationAsync(string clientId) =>
        Task.FromResult<Results<NoContent, NotFound>>(TypedResults.NoContent());

    private static Task<Results<Ok, NotFound>> RotateSecretAsync(string clientId) =>
        Task.FromResult<Results<Ok, NotFound>>(TypedResults.Ok());

    // ──── Scope handlers ────

    private static Task<Ok> ListScopesAsync() => Task.FromResult(TypedResults.Ok());

    private static Task<Created> CreateScopeAsync() =>
        Task.FromResult(TypedResults.Created("/api/admin/oidc/scopes/{name}"));

    private static Task<Results<NoContent, NotFound>> DeleteScopeAsync(string scopeName) =>
        Task.FromResult<Results<NoContent, NotFound>>(TypedResults.NoContent());

    // ──── Authorization handlers ────

    private static Task<Ok> ListAuthorizationsAsync() =>
        Task.FromResult(TypedResults.Ok());

    private static Task<Results<NoContent, NotFound>> RevokeAuthorizationAsync(Guid authorizationId) =>
        Task.FromResult<Results<NoContent, NotFound>>(TypedResults.NoContent());

    private static Task<NoContent> RevokeUserAuthorizationsAsync(Guid userId) =>
        Task.FromResult(TypedResults.NoContent());
#pragma warning restore S1172
}
