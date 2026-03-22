using System.Diagnostics;
using System.Text.Json;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Internal;
using Granit.AI.Exceptions;
using Granit.AI.Workspaces;
using Granit.Guids;
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
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPost("/chat/{workspaceName}/stream", StreamAsync)
            .WithName("AIChatStream")
            .WithSummary("Streams a chat completion response via Server-Sent Events.")
            .WithDescription(
                "Opens an SSE stream that emits incremental content chunks as they arrive from the provider. "
                + "The stream ends with a [DONE] sentinel. "
                + "Returns 404 if the workspace does not exist, or 502 if the provider is unavailable.")
            .Produces<string>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }

    private static async Task<Results<Ok<AIChatResponse>, NotFound, ProblemHttpResult>> CompleteAsync(
        string workspaceName,
        AIChatRequest request,
        [FromServices] IAIChatClientFactory chatClientFactory,
        [FromServices] IAIWorkspaceProvider workspaceProvider,
        [FromServices] IAIUsageTracker usageTracker,
        [FromServices] TimeProvider timeProvider,
        [FromServices] IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await workspaceProvider
            .GetAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        if (workspace is null)
        {
            return TypedResults.NotFound();
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

        var stopwatch = Stopwatch.StartNew();

        ChatResponse response;
        try
        {
            response = await chatClient
                .GetResponseAsync(messages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            return AIProviderExceptionMapper.MapException(ex);
        }

        stopwatch.Stop();

        string content = response.Text ?? string.Empty;
        UsageDetails? usage = response.Usage;

        AIChatUsageResponse? usageResponse = null;
        if (usage is not null)
        {
            usageResponse = new AIChatUsageResponse(
                (int)(usage.InputTokenCount ?? 0),
                (int)(usage.OutputTokenCount ?? 0),
                null);

            await usageTracker.RecordAsync(new AIUsageRecord
            {
                Id = guidGenerator.Create(),
                WorkspaceName = workspaceName,
                Provider = workspace.Provider,
                Model = workspace.Model,
                InputTokens = (int)(usage.InputTokenCount ?? 0),
                OutputTokens = (int)(usage.OutputTokenCount ?? 0),
                Timestamp = timeProvider.GetUtcNow(),
                Duration = stopwatch.Elapsed,
            }, cancellationToken).ConfigureAwait(false);
        }

        return TypedResults.Ok(new AIChatResponse(
            workspaceName,
            workspace.Model,
            content,
            usageResponse,
            stopwatch.Elapsed));
    }

    private static async Task StreamAsync(
        string workspaceName,
        AIChatRequest request,
        [FromServices] IAIChatClientFactory chatClientFactory,
        [FromServices] IAIWorkspaceProvider workspaceProvider,
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

        await foreach (ChatResponseUpdate? update in chatClient.GetStreamingResponseAsync(
            messages, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            foreach (TextContent content in update.Contents.OfType<TextContent>())
            {
                await httpContext.Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(new { content = content.Text })}\n\n",
                    cancellationToken).ConfigureAwait(false);
                await httpContext.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
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
