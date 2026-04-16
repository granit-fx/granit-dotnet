using System.Diagnostics;
using System.Text.Json;
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
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPost("/chat/{workspaceName}/stream", StreamAsync)
            .WithName("AIChatStream")
            .WithSummary("Streams a chat completion response via Server-Sent Events.")
            .WithDescription(
                "Opens an SSE stream that emits incremental content chunks as they arrive from the provider. "
                + "The stream ends with a [DONE] sentinel. If the provider returns an error after streaming has "
                + "started, an SSE 'error' event is emitted before closing. "
                + "Returns 404 if the workspace does not exist, 422 if the model is not found, "
                + "or 502/503 if the provider is unavailable.")
            .Produces<string>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
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

    private static async Task StreamAsync(
        string workspaceName,
        AIChatRequest request,
        [FromServices] IAIChatClientFactory chatClientFactory,
        [FromServices] IAIWorkspaceProvider workspaceProvider,
        [FromServices] IAIUsageTracker usageTracker,
        [FromServices] IAIUsageRecordFactory usageRecordFactory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await workspaceProvider
            .GetAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        if (workspace is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
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
            httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            httpContext.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                httpContext.Response.Body,
                new { detail = ex.Message, status = 502 },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var messages = request.Messages
            .Select(m => new ChatMessage(MapRole(m.Role), m.Content))
            .ToList();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var stopwatch = Stopwatch.StartNew();
        bool headersSent = false;
        UsageContent? accumulatedUsage = null;

        try
        {
            await foreach (ChatResponseUpdate? update in chatClient.GetStreamingResponseAsync(
                messages, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
                {
                    accumulatedUsage = usage;
                }

                foreach (TextContent content in update.Contents.OfType<TextContent>())
                {
                    headersSent = true;
                    await httpContext.Response.WriteAsync(
                        $"data: {JsonSerializer.Serialize(new { content = content.Text })}\n\n",
                        cancellationToken).ConfigureAwait(false);
                    await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            if (!headersSent && !httpContext.Response.HasStarted)
            {
                httpContext.Response.ContentType = "application/problem+json";
                ProblemHttpResult problem = AIProviderExceptionMapper.MapException(
                    ex, workspace.Model, workspace.Provider);
                await problem.ExecuteAsync(httpContext).ConfigureAwait(false);
                return;
            }

            string errorMessage = AIProviderExceptionMapper.GetErrorMessage(
                ex, workspace.Model, workspace.Provider);
            await httpContext.Response.WriteAsync(
                $"event: error\ndata: {JsonSerializer.Serialize(new { error = errorMessage })}\n\n",
                CancellationToken.None).ConfigureAwait(false);
            await httpContext.Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        stopwatch.Stop();

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
                stopwatch.Elapsed);

            await usageTracker.RecordAsync(usageRecord, CancellationToken.None).ConfigureAwait(false);

            await httpContext.Response.WriteAsync(
                $"event: usage\ndata: {JsonSerializer.Serialize(new { inputTokens, outputTokens })}\n\n",
                cancellationToken).ConfigureAwait(false);
            await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken)
            .ConfigureAwait(false);
        await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ChatRole MapRole(string role) => role switch
    {
        "user" => ChatRole.User,
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        _ => new ChatRole(role),
    };
}
