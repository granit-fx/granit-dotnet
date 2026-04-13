using System.Security.Cryptography;
using Granit.Http.Idempotency.Attributes;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AdminOidcEndpoints
{
    internal static RouteGroupBuilder MapAdminOidcEndpoints(this RouteGroupBuilder group)
    {
        // ──── Applications ────
        RouteGroupBuilder apps = group.MapGranitGroup("/oidc/applications");

        apps.MapGet("/", ListApplicationsAsync)
            .WithName("ListOidcApplications")
            .WithSummary("Returns all OIDC applications.")
            .WithDescription("Returns all registered OIDC client applications with their client ID, display name, type, and tenant association. Applications are either public (SPA, mobile) or confidential (server-side). Use this list to manage the registered clients in the admin panel.")
            .Produces<IReadOnlyList<AdminOidcApplicationResponse>>()
            .RequireAuthorization(OpenIddictPermissions.Applications.Read);

        apps.MapPost("/", CreateApplicationAsync)
            .WithName("CreateOidcApplication")
            .WithSummary("Creates a new OIDC application.")
            .WithDescription("Registers a new OIDC client with the specified permissions and redirect URIs. For confidential clients, a client secret is generated and returned once in the response. Returns 409 Conflict if a client with the same client ID already exists.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcApplicationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Applications.Manage);

        apps.MapDelete("/{clientId}", DeleteApplicationAsync)
            .WithName("DeleteOidcApplication")
            .WithSummary("Deletes an OIDC application.")
            .WithDescription("Removes the OIDC client and all associated authorizations and tokens.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Applications.Manage);

#pragma warning disable GRSEC003 // Endpoint path constant, not a secret
        apps.MapPost("/{clientId}/rotate-secret", RotateSecretAsync)
#pragma warning restore GRSEC003
            .WithName("RotateOidcApplicationSecret")
            .WithSummary("Rotates an OIDC application's client secret.")
            .WithDescription(
                "Generates a new client secret, invalidating the old one immediately. "
                + "The new plaintext secret is returned once in the response (never stored in plaintext).")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcRotateSecretResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Applications.Rotate);

        // ──── Scopes ────
        RouteGroupBuilder scopes = group.MapGranitGroup("/oidc/scopes");

        scopes.MapGet("/", ListScopesAsync)
            .WithName("ListOidcScopes")
            .WithSummary("Returns all OIDC scopes.")
            .WithDescription("Returns all registered OIDC scopes with their name, display name, and associated resources. Scopes define the claims and resources that tokens can grant access to. Use this endpoint to audit which scopes are available for client configuration.")
            .Produces<IReadOnlyList<AdminOidcScopeResponse>>()
            .RequireAuthorization(OpenIddictPermissions.Scopes.Read);

        scopes.MapPost("/", CreateScopeAsync)
            .WithName("CreateOidcScope")
            .WithSummary("Creates a new OIDC scope.")
            .WithDescription("Registers a new OIDC scope with the specified name, display name, and associated resources. The scope name must be unique. Returns 409 Conflict if a scope with the same name already exists.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcScopeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Scopes.Manage);

        scopes.MapDelete("/{scopeName}", DeleteScopeAsync)
            .WithName("DeleteOidcScope")
            .WithSummary("Deletes an OIDC scope.")
            .WithDescription("Removes the scope. Existing authorizations using this scope are not affected.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Scopes.Manage);

        // ──── Authorizations ────
        RouteGroupBuilder auths = group.MapGranitGroup("/oidc/authorizations");

        auths.MapGet("/", ListAuthorizationsAsync)
            .WithName("ListOidcAuthorizations")
            .WithSummary("Returns OIDC authorizations.")
            .WithDescription("Returns OIDC authorizations filterable by user ID and client ID. Each authorization represents a user's consent grant to an application. Includes the authorization status (valid, revoked) and type (permanent, ad-hoc).")
            .Produces<IReadOnlyList<AdminOidcAuthorizationResponse>>()
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Read);

        auths.MapDelete("/{authorizationId:guid}", RevokeAuthorizationAsync)
            .WithName("RevokeOidcAuthorization")
            .WithSummary("Revokes an OIDC authorization.")
            .WithDescription("Revokes the authorization and all associated tokens. The user will need to re-authorize on next login. Returns 404 if the authorization does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Revoke);

        auths.MapDelete("/user/{userId:guid}", RevokeUserAuthorizationsAsync)
            .WithName("RevokeUserOidcAuthorizations")
            .WithSummary("Revokes all OIDC authorizations for a user.")
            .WithDescription("Revokes all authorizations and tokens for the specified user. Used for GDPR erasure and security incidents.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Revoke);

        return group;
    }

    // ──── Application handlers ────

    private static async Task<Ok<IReadOnlyList<AdminOidcApplicationResponse>>> ListApplicationsAsync(
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminOidcApplicationResponse>();

        await foreach (object app in applicationManager.ListAsync(100, 0, cancellationToken).ConfigureAwait(false))
        {
            string? clientId = await applicationManager.GetClientIdAsync(app, cancellationToken).ConfigureAwait(false);
            string? displayName = await applicationManager.GetDisplayNameAsync(app, cancellationToken).ConfigureAwait(false);
            string? type = await applicationManager.GetApplicationTypeAsync(app, cancellationToken).ConfigureAwait(false);

            Guid? tenantId = app is GranitOpenIddictApplication granitApp ? granitApp.TenantId : null;
            results.Add(new AdminOidcApplicationResponse(clientId, displayName, type, tenantId));
        }

        return TypedResults.Ok<IReadOnlyList<AdminOidcApplicationResponse>>(results);
    }

    private static async Task<Created<AdminOidcApplicationResponse>> CreateApplicationAsync(
        AdminOidcCreateApplicationRequest request,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ApplicationType = request.Type ?? OpenIddictConstants.ApplicationTypes.Web,
        };

        if (!string.IsNullOrEmpty(request.ClientSecret))
        {
            descriptor.ClientSecret = request.ClientSecret;
            descriptor.ClientType = OpenIddictConstants.ClientTypes.Confidential;
        }
        else
        {
            descriptor.ClientType = OpenIddictConstants.ClientTypes.Public;
        }

        object app = await applicationManager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        string? clientId = await applicationManager.GetClientIdAsync(app, cancellationToken).ConfigureAwait(false);
        string? displayName = await applicationManager.GetDisplayNameAsync(app, cancellationToken).ConfigureAwait(false);
        string? type = await applicationManager.GetApplicationTypeAsync(app, cancellationToken).ConfigureAwait(false);
        Guid? tenantId = app is GranitOpenIddictApplication granitApp ? granitApp.TenantId : null;

        return TypedResults.Created(
            $"/admin/oidc/applications/{clientId}",
            new AdminOidcApplicationResponse(clientId, displayName, type, tenantId));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteApplicationAsync(
        string clientId,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        object? app = await applicationManager.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        await applicationManager.DeleteAsync(app, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

#pragma warning disable GRSEC003 // Secret generation and rotation logic, not a stored secret
    private static async Task<Results<Ok<AdminOidcRotateSecretResponse>, ProblemHttpResult>> RotateSecretAsync(
        string clientId,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        object? app = await applicationManager.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        // Generate a cryptographically random 256-bit secret
        string newSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, app, cancellationToken).ConfigureAwait(false);
        descriptor.ClientSecret = newSecret;
        descriptor.ClientType = OpenIddictConstants.ClientTypes.Confidential;

        await applicationManager.UpdateAsync(app, descriptor, cancellationToken).ConfigureAwait(false);

        string? displayName = await applicationManager.GetDisplayNameAsync(app, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new AdminOidcRotateSecretResponse(clientId, displayName, newSecret));
    }
#pragma warning restore GRSEC003

    // ──── Scope handlers ────

    private static async Task<Ok<IReadOnlyList<AdminOidcScopeResponse>>> ListScopesAsync(
        [FromServices] IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminOidcScopeResponse>();

        await foreach (object scope in scopeManager.ListAsync(100, 0, cancellationToken).ConfigureAwait(false))
        {
            string? name = await scopeManager.GetNameAsync(scope, cancellationToken).ConfigureAwait(false);
            string? displayName = await scopeManager.GetDisplayNameAsync(scope, cancellationToken).ConfigureAwait(false);
            string? description = await scopeManager.GetDescriptionAsync(scope, cancellationToken).ConfigureAwait(false);
            results.Add(new AdminOidcScopeResponse(name, displayName, description));
        }

        return TypedResults.Ok<IReadOnlyList<AdminOidcScopeResponse>>(results);
    }

    private static async Task<Created<AdminOidcScopeResponse>> CreateScopeAsync(
        AdminOidcCreateScopeRequest request,
        [FromServices] IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
        };

        object scope = await scopeManager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        string? name = await scopeManager.GetNameAsync(scope, cancellationToken).ConfigureAwait(false);
        string? displayName = await scopeManager.GetDisplayNameAsync(scope, cancellationToken).ConfigureAwait(false);
        string? description = await scopeManager.GetDescriptionAsync(scope, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/admin/oidc/scopes/{name}",
            new AdminOidcScopeResponse(name, displayName, description));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteScopeAsync(
        string scopeName,
        [FromServices] IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        object? scope = await scopeManager.FindByNameAsync(scopeName, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        await scopeManager.DeleteAsync(scope, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    // ──── Authorization handlers ────

    private static async Task<Ok<IReadOnlyList<AdminOidcAuthorizationResponse>>> ListAuthorizationsAsync(
        [FromServices] IOpenIddictAuthorizationManager authorizationManager,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminOidcAuthorizationResponse>();

        await foreach (object auth in authorizationManager.ListAsync(100, 0, cancellationToken).ConfigureAwait(false))
        {
            string? id = await authorizationManager.GetIdAsync(auth, cancellationToken).ConfigureAwait(false);
            string? subject = await authorizationManager.GetSubjectAsync(auth, cancellationToken).ConfigureAwait(false);
            string? status = await authorizationManager.GetStatusAsync(auth, cancellationToken).ConfigureAwait(false);
            string? type = await authorizationManager.GetTypeAsync(auth, cancellationToken).ConfigureAwait(false);

            results.Add(new AdminOidcAuthorizationResponse(
                Guid.TryParse(id, out Guid parsedId) ? parsedId : Guid.Empty,
                subject, null, status, type));
        }

        return TypedResults.Ok<IReadOnlyList<AdminOidcAuthorizationResponse>>(results);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeAuthorizationAsync(
        Guid authorizationId,
        [FromServices] IOpenIddictAuthorizationManager authorizationManager,
        [FromServices] IOpenIddictTokenManager tokenManager,
        CancellationToken cancellationToken)
    {
        object? auth = await authorizationManager.FindByIdAsync(
            authorizationId.ToString(), cancellationToken).ConfigureAwait(false);

        if (auth is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        // Revoke all tokens associated with this authorization
        await foreach (object token in tokenManager.FindByAuthorizationIdAsync(
            authorizationId.ToString(), cancellationToken).ConfigureAwait(false))
        {
            await tokenManager.TryRevokeAsync(token, cancellationToken).ConfigureAwait(false);
        }

        await authorizationManager.DeleteAsync(auth, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RevokeUserAuthorizationsAsync(
        Guid userId,
        [FromServices] IOpenIddictAuthorizationManager authorizationManager,
        [FromServices] IOpenIddictTokenManager tokenManager,
        CancellationToken cancellationToken)
    {
        string subject = userId.ToString();

        // Revoke all tokens for this user (security incident / GDPR erasure)
        await foreach (object token in tokenManager.FindBySubjectAsync(
            subject, cancellationToken).ConfigureAwait(false))
        {
            await tokenManager.TryRevokeAsync(token, cancellationToken).ConfigureAwait(false);
        }

        // Revoke all authorizations for this user
        await foreach (object authorization in authorizationManager.FindBySubjectAsync(
            subject, cancellationToken).ConfigureAwait(false))
        {
            await authorizationManager.DeleteAsync(authorization, cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.NoContent();
    }
}
