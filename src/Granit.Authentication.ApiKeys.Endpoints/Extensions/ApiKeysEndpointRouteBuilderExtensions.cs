using Granit.Authentication.ApiKeys.Endpoints.Endpoints;
using Granit.Authentication.ApiKeys.Endpoints.Options;
using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    /// <item>List and get API keys (<c>ApiKeys.Keys.Read</c> permission)</item>
    /// <item>Create API keys (<c>ApiKeys.Keys.Create</c> permission)</item>
    /// <item>Revoke API keys (<c>ApiKeys.Keys.Revoke</c> permission)</item>
    /// <item>Rotate API keys (<c>ApiKeys.Keys.Rotate</c> permission)</item>
    /// <item>Update scopes (<c>ApiKeys.Keys.UpdateScopes</c> permission)</item>
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

        // Register fallback authorization policies
        IOptions<AuthorizationOptions> authOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();

        authOptions.Value.AddPolicy(
            ApiKeyPermissions.Keys.Read,
            policy => policy.RequireRole(options.RequiredRole));
        authOptions.Value.AddPolicy(
            ApiKeyPermissions.Keys.Create,
            policy => policy.RequireRole(options.RequiredRole));
        authOptions.Value.AddPolicy(
            ApiKeyPermissions.Keys.Revoke,
            policy => policy.RequireRole(options.RequiredRole));
        authOptions.Value.AddPolicy(
            ApiKeyPermissions.Keys.Rotate,
            policy => policy.RequireRole(options.RequiredRole));
        authOptions.Value.AddPolicy(
            ApiKeyPermissions.Keys.UpdateScopes,
            policy => policy.RequireRole(options.RequiredRole));

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
