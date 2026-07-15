using System.Security.Cryptography;
using Granit.Domain;
using Granit.Entities;
using Granit.Http.Idempotency;
using Granit.MultiTenancy;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Models;
using Granit.OpenIddict.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AdminOidcEndpoints
{
    internal static RouteGroupBuilder MapAdminOidcEndpoints(this RouteGroupBuilder group)
    {
        // ──── Applications ────
        RouteGroupBuilder apps = group.MapGranitGroup("/oidc/applications");

        apps.MapGet("/", ListApplicationsAsync)
            .WithMetadata(new EntityEndpointMetadata(typeof(OpenIddictApplicationModel), EntityEndpointKind.List))
            .WithName("ListOidcApplications")
            .WithSummary("Returns all OIDC applications.")
            .WithDescription("Returns all registered OIDC client applications with their full configuration: client ID, display name, type, tenant, permissions, redirect URIs, consent type, and signing-key presence. Use this list to manage the registered clients in the admin panel.")
            .Produces<IReadOnlyList<AdminOidcApplicationResponse>>()
            .RequireAuthorization(OpenIddictPermissions.Applications.Read);

        apps.MapGet("/{clientId}", GetApplicationAsync)
            .WithName("GetOidcApplication")
            .WithSummary("Returns a single OIDC application by client ID.")
            .WithDescription("Returns the full configuration of the specified OIDC client application. Returns 404 if no application with the given client ID exists.")
            .Produces<AdminOidcApplicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Applications.Read);

        apps.MapPost("/", CreateApplicationAsync)
            .WithName("CreateOidcApplication")
            .WithSummary("Creates a new OIDC application.")
            .WithDescription("Registers a new OIDC client with the specified permissions, redirect URIs, and consent policy. For confidential clients, a client secret is generated and returned once in the response. Returns 409 Conflict if a client with the same client ID already exists.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcApplicationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(OpenIddictPermissions.Applications.Manage);

        apps.MapPut("/{clientId}", UpdateApplicationAsync)
            .WithName("UpdateOidcApplication")
            .WithSummary("Updates an OIDC application.")
            .WithDescription("Updates the configuration of an existing OIDC application. Only non-null fields are applied; null leaves the existing value unchanged. Pass an empty array to clear a collection. Returns 404 if the application does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcApplicationResponse>()
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
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
            .WithMetadata(new EntityEndpointMetadata(typeof(OpenIddictScopeModel), EntityEndpointKind.List))
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
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(OpenIddictPermissions.Scopes.Manage);

        scopes.MapPut("/{scopeName}", UpdateScopeAsync)
            .WithName("UpdateOidcScope")
            .WithSummary("Updates an OIDC scope.")
            .WithDescription("Updates the display name, description, or resource server identifiers of an existing OIDC scope. Null fields are left unchanged; an empty Resources array clears all resources. Returns 404 if the scope does not exist.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcScopeResponse>()
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
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

        auths.MapPost("/", CreateAuthorizationAsync)
            .WithName("CreateOidcAuthorization")
            .WithSummary("Creates an OIDC authorization on behalf of a subject.")
            .WithDescription("Pre-grants consent for a subject (user ID) to a client application with the specified scopes. Useful for admin-driven consent flows where the user cannot complete the interactive consent page.")
            .WithMetadata(new IdempotentAttribute { Required = false })
            .Produces<AdminOidcAuthorizationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Authorizations.Create);

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

    private static async Task<Results<Ok<AdminOidcApplicationResponse>, ProblemHttpResult>> GetApplicationAsync(
        string clientId,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        object? app = await applicationManager.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, app, cancellationToken).ConfigureAwait(false);
        Guid? tenantId = app is IMultiTenant granitApp ? granitApp.TenantId : null;

        return TypedResults.Ok(ToResponse(descriptor, tenantId));
    }

    private static async Task<Ok<IReadOnlyList<AdminOidcApplicationResponse>>> ListApplicationsAsync(
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminOidcApplicationResponse>();

        await foreach (object app in applicationManager.ListAsync(100, 0, cancellationToken).ConfigureAwait(false))
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, app, cancellationToken).ConfigureAwait(false);
            Guid? tenantId = app is IMultiTenant granitApp ? granitApp.TenantId : null;
            results.Add(ToResponse(descriptor, tenantId));
        }

        return TypedResults.Ok<IReadOnlyList<AdminOidcApplicationResponse>>(results);
    }

    private static async Task<Results<Created<AdminOidcApplicationResponse>, ProblemHttpResult>> CreateApplicationAsync(
        AdminOidcCreateApplicationRequest request,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!TryResolveWriteTenant(request.TenantId, currentTenant, out Guid? tenantId))
        {
            return TypedResults.Problem(
                detail: "A tenant-scoped administrator cannot assign an application to a different tenant.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // ClientId is globally unique; a duplicate would otherwise surface as a 500 from the
        // manager's unique-constraint violation. Report the documented 409 instead.
        if (await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return TypedResults.Problem(
                detail: $"An OIDC application with client ID '{request.ClientId}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            DisplayName = request.DisplayName,
            ApplicationType = request.Type ?? OpenIddictConstants.ApplicationTypes.Web,
            ConsentType = request.ConsentType ?? OpenIddictConstants.ConsentTypes.Implicit,
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

        foreach (string permission in request.Permissions ?? [])
        {
            descriptor.Permissions.Add(permission);
        }

        foreach (string uri in request.RedirectUris ?? [])
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (string uri in request.PostLogoutRedirectUris ?? [])
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        if (!string.IsNullOrEmpty(request.SigningKeyJwk))
        {
            descriptor.JsonWebKeySet = BuildJsonWebKeySet(request.SigningKeyJwk);
        }

        descriptor.SetClientSide(request.ClientSide);
        descriptor.SetDeviceKind(request.DeviceKind);

        object app = await applicationManager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        // OpenIddict entities implement IMultiTenant but are not audited, so the persistence
        // interceptor (which stamps only ICreationAuditedObject entities) never assigns their
        // TenantId — stamp the resolved tenant explicitly. Tokens and authorizations stay global.
        if (tenantId is not null && app is IMultiTenant granitApp && granitApp.TenantId != tenantId)
        {
            granitApp.TenantId = tenantId;
            await applicationManager.UpdateAsync(app, cancellationToken).ConfigureAwait(false);
        }

        var responseDescriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(responseDescriptor, app, cancellationToken).ConfigureAwait(false);
        Guid? persistedTenantId = app is IMultiTenant persisted ? persisted.TenantId : null;

        return TypedResults.Created(
            $"/admin/oidc/applications/{request.ClientId}",
            ToResponse(responseDescriptor, persistedTenantId));
    }

    /// <summary>
    /// Resolves the owning tenant for a write. An explicit tenant wins in host context
    /// (no active tenant); otherwise the caller's active tenant is used. A tenant-scoped
    /// caller that names a <em>different</em> tenant is rejected (cross-tenant provisioning).
    /// </summary>
    /// <param name="explicitTenantId">The tenant named on the request, or <see langword="null"/>.</param>
    /// <param name="currentTenant">The ambient tenant context.</param>
    /// <param name="resolved">The tenant to stamp (<see langword="null"/> = global) when the call is allowed.</param>
    /// <returns><see langword="false"/> when the resolution is a forbidden cross-tenant write.</returns>
    internal static bool TryResolveWriteTenant(
        Guid? explicitTenantId, ICurrentTenant currentTenant, out Guid? resolved)
    {
        Guid? activeTenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        if (explicitTenantId is not null
            && activeTenantId is not null
            && explicitTenantId != activeTenantId)
        {
            resolved = null;
            return false;
        }

        resolved = explicitTenantId ?? activeTenantId;
        return true;
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

    private static async Task<Results<Ok<AdminOidcApplicationResponse>, ProblemHttpResult>> UpdateApplicationAsync(
        string clientId,
        AdminOidcUpdateApplicationRequest request,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        object? app = await applicationManager.FindByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, app, cancellationToken).ConfigureAwait(false);

        ApplyRequestedUpdates(descriptor, request);

        await applicationManager.UpdateAsync(app, descriptor, cancellationToken).ConfigureAwait(false);

        var responseDescriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(responseDescriptor, app, cancellationToken).ConfigureAwait(false);
        Guid? tenantId = app is IMultiTenant granitApp ? granitApp.TenantId : null;

        return TypedResults.Ok(ToResponse(responseDescriptor, tenantId));
    }

    // Applies the non-null fields of the update request onto the descriptor. A null field
    // leaves the existing value untouched; a present collection fully replaces the existing one.
    private static void ApplyRequestedUpdates(
        OpenIddictApplicationDescriptor descriptor, AdminOidcUpdateApplicationRequest request)
    {
        if (request.DisplayName is not null)
        {
            descriptor.DisplayName = request.DisplayName;
        }

        if (request.Type is not null)
        {
            descriptor.ApplicationType = request.Type;
        }

        if (request.ConsentType is not null)
        {
            descriptor.ConsentType = request.ConsentType;
        }

        ReplaceCollection(descriptor.Permissions, request.Permissions, static p => p);
        ReplaceCollection(descriptor.RedirectUris, request.RedirectUris, static u => new Uri(u));
        ReplaceCollection(descriptor.PostLogoutRedirectUris, request.PostLogoutRedirectUris, static u => new Uri(u));

        if (request.SigningKeyJwk is not null)
        {
            // Empty string = clear the key; non-empty = update
            descriptor.JsonWebKeySet = !string.IsNullOrEmpty(request.SigningKeyJwk)
                ? BuildJsonWebKeySet(request.SigningKeyJwk)
                : null;
        }

        if (request.ClientSide is not null)
        {
            descriptor.SetClientSide(request.ClientSide.Value);
        }

        if (request.DeviceKind is not null)
        {
            // SetDeviceKind treats Unknown as "clear the declaration".
            descriptor.SetDeviceKind(request.DeviceKind.Value);
        }
    }

    // Replaces the full contents of a descriptor collection from a request field: null leaves it untouched,
    // a present field clears then refills it (mapping each raw string through <paramref name="map"/>).
    private static void ReplaceCollection<T>(
        ICollection<T> target, IReadOnlyList<string>? source, Func<string, T> map)
    {
        if (source is null)
        {
            return;
        }

        target.Clear();
        foreach (string item in source)
        {
            target.Add(map(item));
        }
    }

    // ──── Scope handlers ────

    private static async Task<Ok<IReadOnlyList<AdminOidcScopeResponse>>> ListScopesAsync(
        [FromServices] IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminOidcScopeResponse>();

        await foreach (object scope in scopeManager.ListAsync(100, 0, cancellationToken).ConfigureAwait(false))
        {
            var descriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(descriptor, scope, cancellationToken).ConfigureAwait(false);
            Guid? tenantId = scope is IMultiTenant granitScope ? granitScope.TenantId : null;
            results.Add(ToScopeResponse(descriptor, tenantId));
        }

        return TypedResults.Ok<IReadOnlyList<AdminOidcScopeResponse>>(results);
    }

    private static async Task<Results<Created<AdminOidcScopeResponse>, ProblemHttpResult>> CreateScopeAsync(
        AdminOidcCreateScopeRequest request,
        [FromServices] IOpenIddictScopeManager scopeManager,
        [FromServices] ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        if (!TryResolveWriteTenant(request.TenantId, currentTenant, out Guid? tenantId))
        {
            return TypedResults.Problem(
                detail: "A tenant-scoped administrator cannot assign a scope to a different tenant.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // Scope names are unique; a duplicate would otherwise surface as a 500. Report 409.
        if (await scopeManager.FindByNameAsync(request.Name, cancellationToken).ConfigureAwait(false) is not null)
        {
            return TypedResults.Problem(
                detail: $"An OIDC scope named '{request.Name}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
        };

        foreach (string resource in request.Resources ?? [])
        {
            descriptor.Resources.Add(resource);
        }

        object scope = await scopeManager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        // OpenIddict entities implement IMultiTenant but are not audited, so the persistence
        // interceptor never stamps their TenantId — assign the resolved tenant explicitly.
        if (tenantId is not null && scope is IMultiTenant granitScope && granitScope.TenantId != tenantId)
        {
            granitScope.TenantId = tenantId;
            await scopeManager.UpdateAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        var responseDescriptor = new OpenIddictScopeDescriptor();
        await scopeManager.PopulateAsync(responseDescriptor, scope, cancellationToken).ConfigureAwait(false);
        Guid? persistedTenantId = scope is IMultiTenant persisted ? persisted.TenantId : null;

        return TypedResults.Created(
            $"/admin/oidc/scopes/{responseDescriptor.Name}",
            ToScopeResponse(responseDescriptor, persistedTenantId));
    }

    private static async Task<Results<Ok<AdminOidcScopeResponse>, ProblemHttpResult>> UpdateScopeAsync(
        string scopeName,
        AdminOidcUpdateScopeRequest request,
        [FromServices] IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        object? scope = await scopeManager.FindByNameAsync(scopeName, cancellationToken).ConfigureAwait(false);
        if (scope is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        var descriptor = new OpenIddictScopeDescriptor();
        await scopeManager.PopulateAsync(descriptor, scope, cancellationToken).ConfigureAwait(false);

        if (request.DisplayName is not null)
        {
            descriptor.DisplayName = request.DisplayName;
        }

        if (request.Description is not null)
        {
            descriptor.Description = request.Description;
        }

        if (request.Resources is not null)
        {
            descriptor.Resources.Clear();
            foreach (string resource in request.Resources)
            {
                descriptor.Resources.Add(resource);
            }
        }

        await scopeManager.UpdateAsync(scope, descriptor, cancellationToken).ConfigureAwait(false);

        var responseDescriptor = new OpenIddictScopeDescriptor();
        await scopeManager.PopulateAsync(responseDescriptor, scope, cancellationToken).ConfigureAwait(false);
        Guid? tenantId = scope is IMultiTenant granitScope ? granitScope.TenantId : null;

        return TypedResults.Ok(ToScopeResponse(responseDescriptor, tenantId));
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

    private static async Task<Results<Created<AdminOidcAuthorizationResponse>, ProblemHttpResult>> CreateAuthorizationAsync(
        AdminOidcCreateAuthorizationRequest request,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        [FromServices] IOpenIddictAuthorizationManager authorizationManager,
        CancellationToken cancellationToken)
    {
        // Resolve the application's internal ID from its OAuth client_id.
        object? app = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken).ConfigureAwait(false);
        if (app is null)
        {
            return TypedResults.Problem(
                detail: $"No application found with client_id '{request.ClientId}'.",
                statusCode: StatusCodes.Status404NotFound);
        }

        string? applicationId = await applicationManager.GetIdAsync(app, cancellationToken).ConfigureAwait(false);

        var descriptor = new OpenIddictAuthorizationDescriptor
        {
            Subject = request.Subject,
            ApplicationId = applicationId,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Status = OpenIddictConstants.Statuses.Valid,
        };
        foreach (string scope in request.Scopes)
        {
            descriptor.Scopes.Add(scope);
        }

        object auth = await authorizationManager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        string? id = await authorizationManager.GetIdAsync(auth, cancellationToken).ConfigureAwait(false);

        return TypedResults.Created(
            $"/admin/oidc/authorizations/{id}",
            new AdminOidcAuthorizationResponse(
                Guid.TryParse(id, out Guid parsedId) ? parsedId : Guid.Empty,
                request.Subject, request.ClientId,
                descriptor.Status, descriptor.Type,
                [.. descriptor.Scopes]));
    }

    private static async Task<Ok<IReadOnlyList<AdminOidcAuthorizationResponse>>> ListAuthorizationsAsync(
        [FromServices] IOpenIddictAuthorizationManager authorizationManager,
        [FromServices] IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var results = new List<AdminOidcAuthorizationResponse>();
        var clientIdCache = new Dictionary<string, string?>(StringComparer.Ordinal);

        await foreach (object auth in authorizationManager.ListAsync(100, 0, cancellationToken).ConfigureAwait(false))
        {
            string? id = await authorizationManager.GetIdAsync(auth, cancellationToken).ConfigureAwait(false);
            var descriptor = new OpenIddictAuthorizationDescriptor();
            await authorizationManager.PopulateAsync(descriptor, auth, cancellationToken).ConfigureAwait(false);

            string? clientId = null;
            if (descriptor.ApplicationId is not null
                && !clientIdCache.TryGetValue(descriptor.ApplicationId, out clientId))
            {
                object? app = await applicationManager.FindByIdAsync(descriptor.ApplicationId, cancellationToken).ConfigureAwait(false);
                clientId = app is not null
                    ? await applicationManager.GetClientIdAsync(app, cancellationToken).ConfigureAwait(false)
                    : null;
                clientIdCache[descriptor.ApplicationId] = clientId;
            }

            results.Add(new AdminOidcAuthorizationResponse(
                Guid.TryParse(id, out Guid parsedId) ? parsedId : Guid.Empty,
                descriptor.Subject, clientId, descriptor.Status, descriptor.Type,
                [.. descriptor.Scopes]));
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

    // ──── Helpers ────

    private static AdminOidcScopeResponse ToScopeResponse(
        OpenIddictScopeDescriptor descriptor, Guid? tenantId) =>
        new(
            descriptor.Name,
            descriptor.DisplayName,
            descriptor.Description,
            [.. descriptor.Resources],
            tenantId);

    private static AdminOidcApplicationResponse ToResponse(
        OpenIddictApplicationDescriptor descriptor, Guid? tenantId) =>
        new(
            descriptor.ClientId,
            descriptor.DisplayName,
            descriptor.ApplicationType,
            tenantId,
            [.. descriptor.Permissions],
            [.. descriptor.RedirectUris.Select(u => u.ToString())],
            [.. descriptor.PostLogoutRedirectUris.Select(u => u.ToString())],
            descriptor.ConsentType,
            descriptor.GetClientSide(),
            descriptor.GetDeviceKind(),
            descriptor.JsonWebKeySet is not null);

    /// <summary>
    /// Builds a <see cref="JsonWebKeySet"/> from a JWK JSON string,
    /// stripping private key parameters so only the public key is stored.
    /// </summary>
    private static JsonWebKeySet BuildJsonWebKeySet(string jwkJson)
    {
        var jwk = new JsonWebKey(jwkJson);

        // Strip private key parameters — only store the public key
        jwk.D = null;
        jwk.P = null;
        jwk.Q = null;
        jwk.DP = null;
        jwk.DQ = null;
        jwk.QI = null;

        jwk.Use = JsonWebKeyUseNames.Sig;

        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(jwk);
        return jwks;
    }
}
