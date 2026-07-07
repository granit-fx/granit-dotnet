using Granit.Authorization.Domain;
using Granit.Authorization.Endpoints.Endpoints;
using Granit.Authorization.Endpoints.Options;
using Granit.Authorization.Endpoints.Permissions;
using Granit.QueryEngine.Endpoints.Extensions;
using Granit.Validation.AspNetCore;
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
    /// <item><c>GET /{prefix}/permissions</c> — current user's granted permissions (authenticated only)</item>
    /// <item><c>GET /{prefix}/permissions/definitions</c> — all permission definitions (admin)</item>
    /// <item><c>GET/PUT/DELETE /{prefix}/roles/{roleName}/...</c> — grant management (admin)</item>
    /// </list>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitAuthorization();
    ///
    /// // With a custom prefix:
    /// app.MapGranitAuthorization(opts =>
    /// {
    ///     opts.ApiPrefix = "api/v1";
    ///     opts.RoutePrefix = "authorization";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AuthorizationEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitAuthorization(
        this IEndpointRouteBuilder endpoints,
        Action<AuthorizationEndpointsOptions>? configure = null)
    {
        AuthorizationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        RouteGroupBuilder permissionsGroup = group.MapGranitGroup("permissions");
        permissionsGroup.MapMyPermissionsEndpoints();
        permissionsGroup.MapPermissionDefinitionsEndpoints();
        group.MapPermissionGrantEndpoints();

        // Admin query surfaces — paginated/filterable lists for the entity discovery.
        // Routed under their own subgroups so they coexist with the bespoke grant
        // management endpoints under "/roles/{roleName}" without colliding.
        // Cross-tenant reads are fail-closed by default: a host operator with no resolved tenant
        // sees only the host partition. For ISO 27001 cross-tenant authorization review, mark these
        // groups .AllowHostAccess(); a platform admin holding each group's read permission at global
        // scope then reads across tenants, while the filter stays enforced for tenant-scoped callers.
        group.MapGranitGroup("grants").MapGranitQuery<PermissionGrant>(configure: opts => opts.AuthorizationPolicy = AuthorizationEndpointsPermissions.Grants.Manage);

        group.MapGranitGroup("role-metadata").MapGranitQuery<RoleMetadata>(configure: opts => opts.AuthorizationPolicy = AuthorizationEndpointsPermissions.Definitions.Read);

        return group;
    }
}
