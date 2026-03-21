using Granit.OpenIddict.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

internal static class AdminGroupEndpoints
{
    internal static RouteGroupBuilder MapAdminGroupEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder groups = group.MapGroup("/groups");

        groups.MapGet("/", ListGroupsAsync)
            .WithName("ListGroups")
            .WithSummary("Returns all user groups.")
            .WithDescription("Returns a paginated list of user groups for the current tenant.")
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(OpenIddictPermissions.Groups.Read);

        groups.MapPost("/", CreateGroupAsync)
            .WithName("CreateGroup")
            .WithSummary("Creates a new user group.")
            .WithDescription("Creates a group with the specified name. Returns 409 on duplicate name within tenant.")
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem()
            .RequireAuthorization(OpenIddictPermissions.Groups.Create);

        groups.MapDelete("/{groupId:guid}", DeleteGroupAsync)
            .WithName("DeleteGroup")
            .WithSummary("Deletes a user group.")
            .WithDescription("Deletes the group and cascades to all group memberships.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(OpenIddictPermissions.Groups.Delete);

        groups.MapPost("/{groupId:guid}/members", AddMemberAsync)
            .WithName("AddGroupMember")
            .WithSummary("Adds a user to a group.")
            .WithDescription("Associates the specified user with the group.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(OpenIddictPermissions.Groups.Manage);

        groups.MapDelete("/{groupId:guid}/members/{userId:guid}", RemoveMemberAsync)
            .WithName("RemoveGroupMember")
            .WithSummary("Removes a user from a group.")
            .WithDescription("Dissociates the specified user from the group.")
            .Produces(StatusCodes.Status204NoContent)
            .RequireAuthorization(OpenIddictPermissions.Groups.Manage);

        return group;
    }

    private static Task<Ok> ListGroupsAsync() =>
        Task.FromResult(TypedResults.Ok());

    private static Task<Created> CreateGroupAsync() =>
        Task.FromResult(TypedResults.Created("/api/admin/groups/{id}"));

    private static Task<Results<NoContent, NotFound>> DeleteGroupAsync() =>
        Task.FromResult<Results<NoContent, NotFound>>(TypedResults.NoContent());

    private static Task<NoContent> AddMemberAsync() =>
        Task.FromResult(TypedResults.NoContent());

    private static Task<NoContent> RemoveMemberAsync() =>
        Task.FromResult(TypedResults.NoContent());
}
