using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Endpoints for managing roles via the identity provider.
/// </summary>
internal static class IdentityProviderRoleEndpoints
{
    internal static RouteGroupBuilder MapProviderRoleReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetRolesAsync)
            .WithName("GetIdentityProviderRoles")
            .WithSummary("Lists all roles defined in the identity provider.")
            .WithDescription("Returns all roles available in the identity provider (Keycloak realm roles, Cognito groups, etc.).")
            .Produces<IReadOnlyList<IdentityRole>>();

        group.MapGet("/{roleName}/members", GetRoleMembersAsync)
            .WithName("GetIdentityProviderRoleMembers")
            .WithSummary("Lists all users assigned to a specific role.")
            .WithDescription("Returns users who have the specified role assigned.")
            .Produces<IReadOnlyList<IIdentityUser>>();

        return group;
    }

    internal static RouteGroupBuilder MapProviderUserRoleReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserRolesAsync)
            .WithName("GetIdentityProviderUserRoles")
            .WithSummary("Lists roles assigned to a specific user.")
            .WithDescription("Returns all roles currently assigned to the specified user.")
            .Produces<IReadOnlyList<IdentityRole>>();

        return group;
    }

    internal static RouteGroupBuilder MapProviderUserRoleWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{roleName}", AssignRoleAsync)
            .WithName("AssignIdentityProviderRole")
            .WithSummary("Assigns a role to a user.")
            .WithDescription("Assigns the specified role to the user. Idempotent — assigning an already-assigned role has no effect.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{roleName}", RemoveRoleAsync)
            .WithName("RemoveIdentityProviderRole")
            .WithSummary("Removes a role from a user.")
            .WithDescription("Removes the specified role from the user. Idempotent — removing a non-assigned role has no effect.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentityRole>>> GetRolesAsync(
        [FromServices] IIdentityRoleManager roleManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityRole> roles = await roleManager
            .GetRolesAsync(cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(roles);
    }

    private static async Task<Ok<IReadOnlyList<IIdentityUser>>> GetRoleMembersAsync(
        string roleName,
        [FromServices] IIdentityRoleManager roleManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IIdentityUser> members = await roleManager
            .GetRoleMembersAsync(roleName, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(members);
    }

    private static async Task<Ok<IReadOnlyList<IdentityRole>>> GetUserRolesAsync(
        string userId,
        [FromServices] IIdentityRoleManager roleManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityRole> roles = await roleManager
            .GetUserRolesAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(roles);
    }

    private static async Task<NoContent> AssignRoleAsync(
        string userId,
        string roleName,
        [FromServices] IIdentityRoleManager roleManager,
        CancellationToken cancellationToken)
    {
        await roleManager.AssignRoleAsync(userId, roleName, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveRoleAsync(
        string userId,
        string roleName,
        [FromServices] IIdentityRoleManager roleManager,
        CancellationToken cancellationToken)
    {
        await roleManager.RemoveRoleAsync(userId, roleName, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
