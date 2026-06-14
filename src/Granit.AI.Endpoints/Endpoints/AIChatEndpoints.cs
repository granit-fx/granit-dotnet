using System.Diagnostics;
using System.Runtime.CompilerServices;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Internal;
using Granit.AI.Exceptions;
using Granit.AI.Workspaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace Granit.AI.Endpoints.Endpoints;

internal static class AIChatEndpoints
{
    internal static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/chat/{workspaceName}", CompleteAsync)
            .WithName("AIChatComplete")
            .WithSummary("Sends messages to an AI workspace and returns a completion response.")
            .WithDescription(
                "Forwards the conversation to the underlying AI provider configured for the workspace. "
                + "Returns the assistant's reply, token usage, and response duration. "
                + "Returns 404 if the workspace does not exist, or 502 if the provider is unavailable.")
            .Produces<AIChatResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPost("/chat/{workspaceName}/stream", StreamAsync)
            .WithName("AIChatStream")
            .WithSummary("Streams a chat completion response via Server-Sent Events.")
            .WithDescription(
                "Opens an SSE stream that emits incremental 'delta' content frames as they arrive from the "
                + "provider, then a 'usage' frame with the token counts; end-of-stream is the stream closing. "
                + "A provider failure before any content is returned as an HTTP problem; a failure after "
                + "streaming has started is emitted as an 'error' frame. Returns 404 if the workspace does "
                + "not exist, or 502/503 if the provider is unavailable.")
            .Produces<AIChatStreamEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<Results<Ok<AIChatResponse>, ProblemHttpResult>> CompleteAsync(
        string workspaceName,
        AIChatRequest request,
        [FromServices] IAIChatCompletionService completionService,
        CancellationToken cancellationToken)
    {
        var messages = request.Messages
            .Select(m => new AIChatMessage(m.Role, m.Content))
            .ToList();

        AIChatCompletionResult? result;
        try
        {
            result = await completionService
                .CompleteAsync(workspaceName, messages, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AIWorkspaceNotFoundException or AIProviderNotRegisteredException)
        {
            return AIProviderExceptionMapper.MapException(ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            return AIProviderExceptionMapper.MapException(ex);
        }

        if (result is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        AIChatUsageResponse? usageResponse = result.InputTokens is not null
            ? new AIChatUsageResponse(result.InputTokens.Value, result.OutputTokens!.Value, null, null)
            : null;

        return TypedResults.Ok(new AIChatResponse(
            result.WorkspaceName,
            result.Model,
            result.Content,
            usageResponse,
            result.Duration));
    }

    private static async Task<Results<ServerSentEventsResult<AIChatStreamEvent>, ProblemHttpResult>> StreamAsync(
        string workspaceName,
        AIChatRequest request,
        [FromServices] IAIChatClientFactory chatClientFactory,
        [FromServices] IAIWorkspaceProvider workspaceProvider,
        [FromServices] IAIUsageTracker usageTracker,
        [FromServices] IAIUsageRecordFactory usageRecordFactory,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await workspaceProvider
            .GetAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        if (workspace is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        IChatClient chatClient;
        try
        {
            chatClient = await chatClientFactory
                .CreateAsync(workspaceName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AIWorkspaceNotFoundException or AIProviderNotRegisteredException)
        {
            return AIProviderExceptionMapper.MapException(ex);
        }

        var messages = request.Messages
            .Select(m => new ChatMessage(MapRole(m.Role), m.Content))
            .ToList();

        // Peek the first update so a provider failure before any content surfaces as an HTTP problem
        // (rate limit, timeout, model-not-found) rather than a 200 stream — preserving the contract the
        // hand-rolled implementation had via its "headers not sent yet" guard.
        IAsyncEnumerator<ChatResponseUpdate> updates = chatClient
            .GetStreamingResponseAsync(messages, cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        bool hasFirst;
        try
        {
            hasFirst = await updates.MoveNextAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            await updates.DisposeAsync().ConfigureAwait(false);
            chatClient.Dispose();
            return AIProviderExceptionMapper.MapException(ex, workspace.Model, workspace.Provider);
        }

        return TypedResults.ServerSentEvents(StreamUpdatesAsync(
            chatClient, updates, hasFirst, workspace, workspaceName, usageTracker, usageRecordFactory, cancellationToken));
    }

    private static async IAsyncEnumerable<AIChatStreamEvent> StreamUpdatesAsync(
        IChatClient chatClient,
        IAsyncEnumerator<ChatResponseUpdate> updates,
        bool hasFirst,
        AIWorkspace workspace,
        string workspaceName,
        IAIUsageTracker usageTracker,
        IAIUsageRecordFactory usageRecordFactory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using IChatClient client = chatClient;
        await using IAsyncEnumerator<ChatResponseUpdate> enumerator = updates;

        long startTimestamp = Stopwatch.GetTimestamp();
        UsageContent? accumulatedUsage = null;
        string? streamError = null;

        bool hasCurrent = hasFirst;
        while (hasCurrent)
        {
            ChatResponseUpdate update = enumerator.Current;

            foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
            {
                accumulatedUsage = usage;
            }

            foreach (TextContent content in update.Contents.OfType<TextContent>())
            {
                yield return new AIChatStreamEvent("delta", Content: content.Text);
            }

            try
            {
                hasCurrent = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                // A cancelled request has no client left to receive an error frame; just stop.
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                streamError = AIProviderExceptionMapper.GetErrorMessage(ex, workspace.Model, workspace.Provider);
                break;
            }
        }

        if (streamError is not null)
        {
            yield return new AIChatStreamEvent("error", Error: streamError);
            yield break;
        }

        if (accumulatedUsage is not null)
        {
            int inputTokens = (int)(accumulatedUsage.Details.InputTokenCount ?? 0);
            int outputTokens = (int)(accumulatedUsage.Details.OutputTokenCount ?? 0);

            AIUsageRecord usageRecord = usageRecordFactory.Create(
                workspaceName,
                workspace.Provider,
                workspace.Model,
                inputTokens,
                outputTokens,
                Stopwatch.GetElapsedTime(startTimestamp));

            await usageTracker.RecordAsync(usageRecord, CancellationToken.None).ConfigureAwait(false);

            yield return new AIChatStreamEvent("usage", InputTokens: inputTokens, OutputTokens: outputTokens);
        }
    }

    private static ChatRole MapRole(string role) => role switch
    {
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        _ => new ChatRole(role),
    };
}
