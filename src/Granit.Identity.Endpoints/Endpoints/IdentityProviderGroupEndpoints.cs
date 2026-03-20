using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Endpoints for managing groups via the identity provider.
/// </summary>
internal static class IdentityProviderGroupEndpoints
{
    internal static RouteGroupBuilder MapProviderGroupReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetGroupsAsync)
            .WithName("GetIdentityProviderGroups")
            .WithSummary("Lists all groups defined in the identity provider.")
            .WithDescription("Returns all groups available in the identity provider, including their hierarchical structure.")
            .Produces<IReadOnlyList<IdentityGroup>>();

        return group;
    }

    internal static RouteGroupBuilder MapProviderUserGroupReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserGroupsAsync)
            .WithName("GetIdentityProviderUserGroups")
            .WithSummary("Lists groups a specific user belongs to.")
            .WithDescription("Returns all groups the specified user is a member of.")
            .Produces<IReadOnlyList<IdentityGroup>>();

        return group;
    }

    internal static RouteGroupBuilder MapProviderUserGroupWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPut("/{groupId}", AddUserToGroupAsync)
            .WithName("AddIdentityProviderUserToGroup")
            .WithSummary("Adds a user to a group.")
            .WithDescription("Adds the specified user to the group. Idempotent — adding a user already in the group has no effect.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{groupId}", RemoveUserFromGroupAsync)
            .WithName("RemoveIdentityProviderUserFromGroup")
            .WithSummary("Removes a user from a group.")
            .WithDescription("Removes the specified user from the group. Idempotent — removing a non-member has no effect.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentityGroup>>> GetGroupsAsync(
        [FromServices] IIdentityGroupManager groupManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityGroup> groups = await groupManager
            .GetGroupsAsync(cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(groups);
    }

    private static async Task<Ok<IReadOnlyList<IdentityGroup>>> GetUserGroupsAsync(
        string userId,
        [FromServices] IIdentityGroupManager groupManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityGroup> groups = await groupManager
            .GetUserGroupsAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(groups);
    }

    private static async Task<NoContent> AddUserToGroupAsync(
        string userId,
        string groupId,
        [FromServices] IIdentityGroupManager groupManager,
        CancellationToken cancellationToken)
    {
        await groupManager.AddUserToGroupAsync(userId, groupId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RemoveUserFromGroupAsync(
        string userId,
        string groupId,
        [FromServices] IIdentityGroupManager groupManager,
        CancellationToken cancellationToken)
    {
        await groupManager.RemoveUserFromGroupAsync(userId, groupId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
