using System.Net.Sockets;
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
using Microsoft.Extensions.Logging;

namespace Granit.AI.Chat.Endpoints.Endpoints;

/// <summary>The send-message endpoint: runs the agentic loop and streams the answer over SSE.</summary>
internal static partial class ChatSendEndpoints
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
                + "'clarification' when applicable). Rejects a non-chat-capable workspace before streaming. "
                + "If the agent fails after the stream is committed (provider quota exhausted, a provider "
                + "5xx/timeout, or any other fault), the stream ends with a terminal 'error' frame carrying "
                + "a machine 'code' (rate_limit / provider_unavailable / server_error) — a frame within the "
                + "200 response, not an HTTP status. A client-cancelled request emits no 'error' frame.")
            .Produces<ChatStreamEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
        [FromServices] ILoggerFactory loggerFactory,
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
        ILogger logger = loggerFactory.CreateLogger("Granit.AI.Chat.Endpoints.Endpoints.ChatSendEndpoints");
        return TypedResults.ServerSentEvents(StreamAsync(handle, chatService, logger, cancellationToken));
    }

    private static async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatSendHandle handle,
        IChatService chatService,
        ILogger logger,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Flush the conversation id first — its frame commits the SSE headers in milliseconds, long
        // before the agent settles, so the client never times out waiting for the first byte.
        yield return new ChatStreamEvent("conversation", ConversationId: handle.ConversationId);

        // A failure mid-stream can no longer become an HTTP problem (the 200 is committed). Drive the
        // turn through a manual enumerator so a thrown frame can be converted to a terminal 'error'
        // frame: C# forbids 'yield' inside a catch, so the catch only captures the error code and the
        // frame is yielded just outside it.
        await using IAsyncEnumerator<ChatTurnUpdate> updates =
            chatService.StreamAsync(handle, cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            ChatTurnUpdate update;
            string? errorCode = null;
            try
            {
                if (!await updates.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                update = updates.Current;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // An expected client abort, not a failure: emit no 'error' frame, let it propagate.
                throw;
            }
            catch (Exception ex)
            {
                errorCode = MapErrorCode(ex);
                LogStreamFailed(logger, ex, handle.ConversationId, errorCode);
                update = null!; // unread: the error-frame branch below short-circuits before the switch.
            }

            if (errorCode is not null)
            {
                // Terminal frame. Content stays null so no raw provider detail leaks; the front maps
                // the code to a localized message. No partial assistant turn was persisted — the chat
                // service writes the assistant turn only after the loop settles (ADR-068).
                yield return new ChatStreamEvent("error", Code: errorCode);
                yield break;
            }

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

    /// <summary>
    /// Classifies a mid-stream failure into one of the closed wire codes the front maps to a localized
    /// message. Provider SDK exceptions are not referenced as types here (this layer takes no provider
    /// dependency); the HTTP status is read structurally so any provider's exception is still classified.
    /// </summary>
    private static string MapErrorCode(Exception exception)
    {
        if (TryGetHttpStatus(exception) is { } status)
        {
            return status switch
            {
                StatusCodes.Status429TooManyRequests => "rate_limit",
                StatusCodes.Status408RequestTimeout or (>= 500 and < 600) => "provider_unavailable",
                _ => "server_error", // other 4xx mid-stream is a server-side fault, not unavailability.
            };
        }

        // No HTTP status: a transport-level failure (no connection, reset, timeout) means the provider
        // is unreachable. A non-client OperationCanceledException reaches here only as a provider-side
        // timeout — a client abort was already re-thrown above.
        return IsTransportFailure(exception) ? "provider_unavailable" : "server_error";
    }

    /// <summary>
    /// Reads an upstream HTTP status from the exception chain: <see cref="HttpRequestException.StatusCode"/>
    /// when typed, else a public <c>int Status</c> property (the shape provider SDK exceptions such as
    /// <c>System.ClientModel.ClientResultException</c> expose), read structurally to avoid a hard
    /// dependency on every provider's SDK. <see langword="null"/> when no status is carried.
    /// </summary>
    private static int? TryGetHttpStatus(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException { StatusCode: { } statusCode })
            {
                return (int)statusCode;
            }

            if (current.GetType().GetProperty("Status")?.GetValue(current) is int status and > 0)
            {
                return status;
            }
        }

        return null;
    }

    /// <summary>Whether the failure (or any inner one) is a transport-level fault — provider unreachable.</summary>
    private static bool IsTransportFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException or IOException or SocketException or TimeoutException or OperationCanceledException)
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Chat send stream failed mid-stream for conversation {ConversationId}; emitting an '{Code}' error frame.")]
    private static partial void LogStreamFailed(ILogger logger, Exception exception, Guid conversationId, string code);

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
