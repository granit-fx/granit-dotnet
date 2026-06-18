using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.QueryEngine;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Chat.Endpoints.Endpoints;

/// <summary>
/// Owner-scoped, backwards-paginated read endpoint for a conversation's message thread. Pages the
/// newest messages first and walks toward older history via an opaque keyset cursor, so the client
/// loads the latest page on open and earlier pages on scroll-up.
/// </summary>
internal static class ChatMessagesEndpoints
{
    internal static RouteGroupBuilder MapChatMessagesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/messages", GetMessagesAsync)
            .WithName("GetConversationMessages")
            .WithSummary("Returns a backwards-paginated page of a conversation's messages.")
            .WithDescription(
                "Returns the newest page of messages first, sorted newest-first. Pass the returned " +
                "nextCursor to load the page of older messages; nextCursor is null at the start of " +
                "history. Scoped to the caller: another user's conversation is reported as not found.")
            .Produces<PagedResult<MessageResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AIChatPermissions.Conversations.Read);

        return group;
    }

    private static async Task<Results<Ok<PagedResult<MessageResponse>>, ProblemHttpResult>> GetMessagesAsync(
        Guid id,
        [FromQuery] string? cursor,
        [FromQuery] int? pageSize,
        [FromServices] IConversationStore store,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return Unauthorized();
        }

        PagedResult<Message>? page = await store
            .GetMessagesPageAsync(id, ownerId, cursor, pageSize, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        // Project the entities to the wire DTO; TotalCount stays null (keyset pagination), the cursor
        // and HasMore flow straight through from the query engine.
        IReadOnlyList<MessageResponse> items = [.. page.Items.Select(MessageResponse.FromEntity)];
        PagedResult<MessageResponse> response = new(items, TotalCount: null, page.HasMore, page.NextCursor);
        return TypedResults.Ok(response);
    }

    private static ProblemHttpResult Unauthorized() =>
        TypedResults.Problem(
            detail: "The current identity has no user context.",
            statusCode: StatusCodes.Status401Unauthorized);
}
