using Granit.Authentication.ApiKeys.Endpoints.Endpoints;
using Granit.Authentication.ApiKeys.Endpoints.Options;
using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authentication.ApiKeys.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering API key management endpoints.
/// </summary>
public static class ApiKeysEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the API key management endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Registers endpoints for:</para>
    /// <list type="bullet">
    /// <item>List and get API keys (<c>AuthenticationApiKeys.Keys.Read</c> permission)</item>
    /// <item>Create API keys (<c>AuthenticationApiKeys.Keys.Create</c> permission)</item>
    /// <item>Revoke API keys (<c>AuthenticationApiKeys.Keys.Revoke</c> permission)</item>
    /// <item>Rotate API keys (<c>AuthenticationApiKeys.Keys.Rotate</c> permission)</item>
    /// <item>Update scopes (<c>AuthenticationApiKeys.Keys.UpdateScopes</c> permission)</item>
    /// </list>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="ApiKeysEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapApiKeysEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<ApiKeysEndpointsOptions>? configure = null)
    {
        ApiKeysEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Read endpoints (list, get by ID)
        group
            .RequireAuthorization(ApiKeyPermissions.Keys.Read)
            .MapReadEndpoints();

        // Create endpoint
        group
            .RequireAuthorization(ApiKeyPermissions.Keys.Create)
            .MapCreateEndpoints();

        // Revoke endpoint
        group
            .RequireAuthorization(ApiKeyPermissions.Keys.Revoke)
            .MapRevokeEndpoints();

        // Rotate endpoint
        group
            .RequireAuthorization(ApiKeyPermissions.Keys.Rotate)
            .MapRotateEndpoints();

        // Update scopes endpoint
        group
            .RequireAuthorization(ApiKeyPermissions.Keys.UpdateScopes)
            .MapScopesEndpoints();

        return group;
    }
}
