using Granit.Identity.Endpoints.Endpoints;
using Granit.Identity.Endpoints.Options;
using Granit.Identity.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering identity provider administration endpoints.
/// </summary>
public static class IdentityProviderEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the identity provider administration endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Registers endpoints for:</para>
    /// <list type="bullet">
    /// <item>User CRUD (<c>Identity.Users.Read</c> / <c>Identity.Users.Manage</c>)</item>
    /// <item>Role management (<c>Identity.Roles.Read</c> / <c>Identity.Roles.Manage</c>)</item>
    /// <item>Group management (<c>Identity.Groups.Read</c> / <c>Identity.Groups.Manage</c>)</item>
    /// <item>Session management (<c>Identity.Sessions.Read</c> / <c>Identity.Sessions.Manage</c>)</item>
    /// <item>Password management (<c>Identity.Passwords.Manage</c>)</item>
    /// </list>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="IdentityProviderEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitIdentityProvider(
        this IEndpointRouteBuilder endpoints,
        Action<IdentityProviderEndpointsOptions>? configure = null)
    {
        IdentityProviderEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // -- Users --
        RouteGroupBuilder usersRead = group.MapGroup("/users")
            .RequireAuthorization(IdentityPermissions.Users.Read);
        usersRead.MapProviderUserReadEndpoints();

        RouteGroupBuilder usersWrite = group.MapGroup("/users")
            .RequireAuthorization(IdentityPermissions.Users.Manage);
        usersWrite.MapProviderUserWriteEndpoints();

        // -- Roles (top-level) --
        RouteGroupBuilder rolesRead = group.MapGroup("/roles")
            .RequireAuthorization(IdentityPermissions.Roles.Read);
        rolesRead.MapProviderRoleReadEndpoints();

        // -- Roles (per-user) --
        RouteGroupBuilder userRolesRead = group.MapGroup("/users/{userId}/roles")
            .RequireAuthorization(IdentityPermissions.Roles.Read);
        userRolesRead.MapProviderUserRoleReadEndpoints();

        RouteGroupBuilder userRolesWrite = group.MapGroup("/users/{userId}/roles")
            .RequireAuthorization(IdentityPermissions.Roles.Manage);
        userRolesWrite.MapProviderUserRoleWriteEndpoints();

        // -- Groups (top-level) --
        RouteGroupBuilder groupsRead = group.MapGroup("/groups")
            .RequireAuthorization(IdentityPermissions.Groups.Read);
        groupsRead.MapProviderGroupReadEndpoints();

        // -- Groups (per-user) --
        RouteGroupBuilder userGroupsRead = group.MapGroup("/users/{userId}/groups")
            .RequireAuthorization(IdentityPermissions.Groups.Read);
        userGroupsRead.MapProviderUserGroupReadEndpoints();

        RouteGroupBuilder userGroupsWrite = group.MapGroup("/users/{userId}/groups")
            .RequireAuthorization(IdentityPermissions.Groups.Manage);
        userGroupsWrite.MapProviderUserGroupWriteEndpoints();

        // -- Sessions --
        RouteGroupBuilder sessionsRead = group.MapGroup("/users/{userId}/sessions")
            .RequireAuthorization(IdentityPermissions.Sessions.Read);
        sessionsRead.MapProviderSessionEndpoints();

        RouteGroupBuilder devicesRead = group.MapGroup("/users/{userId}/devices")
            .RequireAuthorization(IdentityPermissions.Sessions.Read);
        devicesRead.MapProviderDeviceEndpoints();

        // -- Passwords --
        RouteGroupBuilder passwords = group.MapGroup("/users/{userId}/password")
            .RequireAuthorization(IdentityPermissions.Passwords.Manage);
        passwords.MapProviderPasswordEndpoints();

        return group;
    }
}
