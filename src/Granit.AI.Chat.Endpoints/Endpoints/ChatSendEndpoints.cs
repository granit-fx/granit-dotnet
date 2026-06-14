using System.Runtime.CompilerServices;
using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Mentions;
using Granit.AI.Exceptions;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Chat.Endpoints.Endpoints;

/// <summary>The send-message endpoint: runs the agentic loop and streams the answer over SSE.</summary>
internal static class ChatSendEndpoints
{
    internal static RouteGroupBuilder MapChatSendEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/messages", SendAsync)
            .WithName("SendChatMessage")
            .WithSummary("Sends a message and streams a tool-grounded answer over SSE.")
            .WithDescription(
                "Runs the agentic loop within the caller's permissions, persists the user and "
                + "assistant messages, and streams the answer as Server-Sent Events: a 'conversation' "
                + "frame with the (possibly new) conversation id, incremental 'delta' content frames, "
                + "then a 'usage' frame. Rejects a non-chat-capable workspace before streaming.")
            .Produces<ChatStreamEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(AIChatPermissions.Conversations.Send);

        return group;
    }

    private static async Task<Results<ServerSentEventsResult<ChatStreamEvent>, ProblemHttpResult>> SendAsync(
        SendMessageRequest request,
        [FromServices] IChatService chatService,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            return TypedResults.Problem(
                detail: "The current identity has no user context.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        ChatSendResult result;
        try
        {
            result = await chatService.SendAsync(
                new ChatSendRequest
                {
                    ConversationId = request.ConversationId,
                    OwnerId = ownerId,
                    WorkspaceName = request.WorkspaceName,
                    Message = request.Message,
                    Mentions = request.Mentions?.Select(m => new AIMention(m.Type, m.Id)).ToList(),
                    Attachments = request.Attachments?
                        .Select(a => new AIAttachment(a.Reference, a.FileName, a.ContentType, a.SizeBytes)).ToList(),
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceNotChatCapableException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (AIWorkspaceNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConversationNotFoundException ex)
        {
            return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }

        // Native .NET SSE: the framework handles framing, content-type and per-item flushing.
        return TypedResults.ServerSentEvents(StreamAsync(result, cancellationToken));
    }

    private static async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatSendResult result,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ChatStreamEvent("conversation", ConversationId: result.ConversationId);

        foreach (string chunk in Chunk(result.Content))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatStreamEvent("delta", Content: chunk);
            // Yield control between frames so each is flushed as its own SSE event.
            await Task.Yield();
        }

        yield return new ChatStreamEvent("usage", InputTokens: result.InputTokens, OutputTokens: result.OutputTokens);
    }

    /// <summary>Splits text into word-sized chunks (trailing space preserved) for token-like streaming.</summary>
    private static IEnumerable<string> Chunk(string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            int space = text.IndexOf(' ', index);
            if (space < 0)
            {
                yield return text[index..];
                yield break;
            }

            yield return text[index..(space + 1)];
            index = space + 1;
        }
    }
}
