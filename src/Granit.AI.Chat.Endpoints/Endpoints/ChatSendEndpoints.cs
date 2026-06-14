using System.Text.Json;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Exceptions;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                + "event with the (possibly new) conversation id, incremental 'data' content frames, a "
                + "'usage' event, then '[DONE]'. Rejects a non-chat-capable workspace before streaming.")
            .Produces<string>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .RequireAuthorization(AIChatPermissions.Conversations.Send);

        return group;
    }

    private static async Task SendAsync(
        SendMessageRequest request,
        [FromServices] IChatService chatService,
        [FromServices] ICurrentUserService currentUser,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserGuid is not { } ownerId)
        {
            await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized,
                "The current identity has no user context.", cancellationToken).ConfigureAwait(false);
            return;
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
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceNotChatCapableException ex)
        {
            await WriteProblemAsync(httpContext, StatusCodes.Status422UnprocessableEntity, ex.Message, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (AIWorkspaceNotFoundException ex)
        {
            await WriteProblemAsync(httpContext, StatusCodes.Status404NotFound, ex.Message, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (ConversationNotFoundException ex)
        {
            await WriteProblemAsync(httpContext, StatusCodes.Status404NotFound, ex.Message, cancellationToken).ConfigureAwait(false);
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await WriteFrameAsync(httpContext, "conversation",
            JsonSerializer.Serialize(new { conversationId = result.ConversationId }), cancellationToken).ConfigureAwait(false);

        foreach (string chunk in Chunk(result.Content))
        {
            await WriteFrameAsync(httpContext, eventName: null,
                JsonSerializer.Serialize(new { content = chunk }), cancellationToken).ConfigureAwait(false);
        }

        await WriteFrameAsync(httpContext, "usage",
            JsonSerializer.Serialize(new { inputTokens = result.InputTokens, outputTokens = result.OutputTokens }),
            cancellationToken).ConfigureAwait(false);

        await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteFrameAsync(HttpContext httpContext, string? eventName, string json, CancellationToken cancellationToken)
    {
        string frame = eventName is null ? $"data: {json}\n\n" : $"event: {eventName}\ndata: {json}\n\n";
        await httpContext.Response.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int statusCode, string detail, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new { detail, status = statusCode }), cancellationToken).ConfigureAwait(false);
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
