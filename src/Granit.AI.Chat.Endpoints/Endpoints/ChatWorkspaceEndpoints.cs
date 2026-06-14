using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Chat.Endpoints.Endpoints;

/// <summary>Lists the chat-capable workspaces a user may pick as their default.</summary>
internal static class ChatWorkspaceEndpoints
{
    internal static RouteGroupBuilder MapChatWorkspaceEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/workspaces", GetSelectableWorkspacesAsync)
            .WithName("ListChatWorkspaces")
            .WithSummary("Lists the selectable default chat workspaces.")
            .WithDescription(
                "Returns the workspaces a user may set as their default chat workspace: the "
                + "reserved 'Auto' option plus the chat-capable workspaces visible to the tenant. "
                + "Vector/embedding-only workspaces are excluded.")
            .Produces<ChatWorkspacesResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization(AIChatPermissions.Conversations.Read);

        return group;
    }

    private static async Task<Ok<ChatWorkspacesResponse>> GetSelectableWorkspacesAsync(
        [FromServices] IChatWorkspaceCatalog catalog,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> workspaces = await catalog
            .GetSelectableWorkspacesAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new ChatWorkspacesResponse(workspaces));
    }
}
