using Granit.Identity.Local.Endpoints.Endpoints;
using Granit.Identity.Local.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Local.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering the local role CRUD endpoints.
/// </summary>
public static class RoleEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps <c>/admin/roles</c> CRUD endpoints (list / get / create / rename / delete).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="RoleEndpointsOptions"/>.</param>
    /// <returns>The role <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitRoles(
        this IEndpointRouteBuilder endpoints,
        Action<RoleEndpointsOptions>? configure = null)
    {
        RoleEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RolesRoutePrefix)
            .WithTags(options.TagName);

        // Each endpoint declares its own permission via RequireAuthorization(IdentityLocalPermissions.Roles.*),
        // so a parent-level RequireAuthorization() would be redundant.
        group.MapGranitRoleEndpoints();
        return group;
    }
}
