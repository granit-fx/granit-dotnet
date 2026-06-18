using System.Runtime.CompilerServices;
using Granit.AI.Chat.Attachments;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Mentions;
using Granit.AI.Exceptions;
using Granit.Http.RateLimiting.AspNetCore;
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
                + "frame with the (possibly new) conversation id (flushed immediately), then live "
                + "'tool_call'/'tool_result' frames as the agent uses tools and incremental 'delta' "
                + "content frames as the model writes, then a 'usage' frame (and 'suggestions' / "
                + "'clarification' when applicable). Rejects a non-chat-capable workspace before streaming.")
            .Produces<ChatStreamEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization(AIChatPermissions.Conversations.Send)
            // The agentic loop costs real provider spend per call; bound per-user volume to prevent
            // a "denial of wallet". No-op until the host configures the ai-chat-send policy.
            .RequireGranitRateLimiting(AIChatRateLimitPolicies.Send);

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

        // Validate and resolve synchronously so an HTTP problem (404/422) is returned before the SSE
        // stream opens — once the stream is committed we can only emit frames, not a ProblemHttpResult.
        ChatSendHandle handle;
        try
        {
            handle = await chatService.PrepareAsync(
                new ChatSendRequest
                {
                    ConversationId = request.ConversationId,
                    OwnerId = ownerId,
                    WorkspaceName = request.WorkspaceName,
                    Message = request.Message,
                    Mentions = request.Mentions?.Select(m => new AIMention(m.Type, m.Id)).ToList(),
                    Attachments = request.Attachments?
                        .Select(a => new AIAttachment(a.Reference, a.FileName, a.ContentType, a.SizeBytes)).ToList(),
                    PromptRefs = request.PromptRefs,
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
        return TypedResults.ServerSentEvents(StreamAsync(handle, chatService, cancellationToken));
    }

    private static async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatSendHandle handle,
        IChatService chatService,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Flush the conversation id first — its frame commits the SSE headers in milliseconds, long
        // before the agent settles, so the client never times out waiting for the first byte.
        yield return new ChatStreamEvent("conversation", ConversationId: handle.ConversationId);

        await foreach (ChatTurnUpdate update in chatService.StreamAsync(handle, cancellationToken).ConfigureAwait(false))
        {
            switch (update.Kind)
            {
                case ChatTurnUpdateKind.Delta:
                    yield return new ChatStreamEvent("delta", Content: update.Delta);
                    break;

                case ChatTurnUpdateKind.ToolCall:
                    yield return new ChatStreamEvent("tool_call", ToolName: update.ToolName, ToolCallId: update.ToolCallId);
                    break;

                case ChatTurnUpdateKind.ToolResult:
                    yield return new ChatStreamEvent(
                        "tool_result", ToolName: update.ToolName, ToolCallId: update.ToolCallId, Succeeded: update.Succeeded);
                    break;

                case ChatTurnUpdateKind.Completed:
                    foreach (ChatStreamEvent frame in CompletionFrames(update.Result!))
                    {
                        yield return frame;
                    }

                    break;
            }
        }
    }

    /// <summary>The terminal frames derived from the settled result: usage, then any suggestions and clarification.</summary>
    private static IEnumerable<ChatStreamEvent> CompletionFrames(ChatSendResult result)
    {
        yield return new ChatStreamEvent("usage", InputTokens: result.InputTokens, OutputTokens: result.OutputTokens);

        if (result.SuggestedActions.Count > 0)
        {
            yield return new ChatStreamEvent(
                "suggestions",
                SuggestedActions: [.. result.SuggestedActions.Select(a =>
                    new SuggestedActionResponse(a.Type, a.Label, a.DeepLink, a.Description))]);
        }

        if (result.Clarification is { } clarification)
        {
            yield return new ChatStreamEvent(
                "clarification",
                Clarification: new ClarificationResponse(
                    clarification.Question,
                    [.. clarification.Options.Select(o => new ClarificationOptionResponse(o.Label, o.Value))],
                    clarification.AllowOther));
        }
    }
}
