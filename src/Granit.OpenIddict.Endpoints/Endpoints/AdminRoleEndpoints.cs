using Granit.OpenIddict.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AdminRoleEndpoints
{
    internal static RouteGroupBuilder MapAdminRoleEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder roles = group.MapGroup("/roles");

        roles.MapGet("/", ListRolesAsync)
            .WithName("ListRoles")
            .WithSummary("Returns all roles.")
            .WithDescription("Returns the list of all roles with their descriptions.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Roles.Read);

        roles.MapPost("/", CreateRoleAsync)
            .WithName("CreateRole")
            .WithSummary("Creates a new role.")
            .WithDescription("Creates a role with the specified name and description.")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Roles.Create);

        roles.MapDelete("/{roleName}", DeleteRoleAsync)
            .WithName("DeleteRole")
            .WithSummary("Deletes a role.")
            .WithDescription("Returns 409 if the role has members.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(OpenIddictPermissions.Roles.Delete);

        roles.MapGet("/{roleName}/members", GetRoleMembersAsync)
            .WithName("GetRoleMembers")
            .WithSummary("Returns the members of a role.")
            .WithDescription("Returns a paginated list of users assigned to the role.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Roles.Read);

        return group;
    }

    private static Task<Ok> ListRolesAsync() =>
        Task.FromResult(TypedResults.Ok());

    private static Task<Created> CreateRoleAsync() =>
        Task.FromResult(TypedResults.Created("/api/admin/roles/{name}"));

    private static Task<Results<NoContent, ProblemHttpResult>> DeleteRoleAsync(string roleName) =>
        Task.FromResult<Results<NoContent, ProblemHttpResult>>(TypedResults.NoContent());

    private static Task<Ok> GetRoleMembersAsync(string roleName) =>
        Task.FromResult(TypedResults.Ok());
}
