using Granit.Authorization.Endpoints.Endpoints;
using Granit.Authorization.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Authorization.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering authorization management endpoints.
/// </summary>
public static class AuthorizationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the authorization management endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Registers three endpoint groups:</para>
    /// <list type="bullet">
    /// <item><c>GET /{prefix}/me</c> — current user's granted permissions (authenticated only)</item>
    /// <item><c>GET /{prefix}/definitions</c> — all permission definitions (admin)</item>
    /// <item><c>GET/PUT/DELETE /{prefix}/roles/{roleName}/...</c> — grant management (admin)</item>
    /// </list>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapAuthorizationEndpoints();
    ///
    /// // With a custom prefix:
    /// app.MapAuthorizationEndpoints(opts =>
    /// {
    ///     opts.ApiPrefix = "api/v1";
    ///     opts.RoutePrefix = "authorization";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuthorizationEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapAuthorizationEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<AuthorizationEndpointsOptions>? configure = null)
    {
        AuthorizationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        group.MapMyPermissionsEndpoints();
        group.MapPermissionDefinitionsEndpoints();
        group.MapPermissionGrantEndpoints();

        return group;
    }
}
