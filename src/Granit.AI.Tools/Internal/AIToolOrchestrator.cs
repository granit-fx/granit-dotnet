using System.Diagnostics;
using System.Text.Json;
using Granit.AI.Exceptions;
using Granit.AI.Options;
using Granit.AI.Tools.Diagnostics;
using Granit.AI.Tools.Options;
using Granit.AI.Tools.Prompts;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Default <see cref="IAIToolOrchestrator"/>. Runs the think → call → execute → repeat loop
/// against the workspace's <see cref="IChatClient"/>, executing tools through the registry,
/// bounding iterations and tool-result size, and stamping usage on completion (ADR-067).
/// </summary>
internal sealed partial class AIToolOrchestrator(
    IAIChatClientFactory chatClientFactory,
    IAIWorkspaceProvider workspaceProvider,
    IAIToolProjector projector,
    IAIToolRegistry registry,
    IAIToolAuthorizer toolAuthorizer,
    IAISystemPromptComposer systemPromptComposer,
    IAIUsageRecordFactory usageRecordFactory,
    IAIUsageTracker usageTracker,
    IOptions<GranitAIToolsOrchestrationOptions> orchestrationOptions,
    IOptions<GranitAIOptions> aiOptions,
    AIToolsMetrics metrics,
    TimeProvider timeProvider,
    ILogger<AIToolOrchestrator> logger) : IAIToolOrchestrator
{
    private static readonly JsonSerializerOptions ArgumentSerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly JsonElement EmptyArguments =
        JsonDocument.Parse("{}").RootElement.Clone();

    public async Task<AIOrchestrationResult> RunAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        GranitAIToolsOrchestrationOptions options = orchestrationOptions.Value;
        int maxIterations = Math.Max(1, options.MaxIterations);

        string workspaceName = request.WorkspaceName ?? aiOptions.Value.DefaultWorkspace;
        AIWorkspace workspace = await workspaceProvider.GetAsync(workspaceName, cancellationToken).ConfigureAwait(false)
            ?? throw new AIWorkspaceNotFoundException(workspaceName);
        string? tenantId = workspace.TenantId?.ToString();

        // Gate the candidate set to what the caller may use, so unauthorized tools are never
        // declared to the model nor executable this run (per-tool permission gate, ADR-067).
        IReadOnlyList<IAITool> tools = await toolAuthorizer
            .FilterAuthorizedAsync(request.Tools ?? registry.Tools, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<string, IAITool> toolMap = new(tools.Count, StringComparer.Ordinal);
        foreach (IAITool tool in tools)
        {
            toolMap[tool.Name] = tool;
        }

        ChatOptions chatOptions = new();
        if (toolMap.Count > 0)
        {
            chatOptions.Tools = [.. projector.Project(tools)];
            chatOptions.ToolMode = ChatToolMode.Auto;
        }

        AISystemPrompt systemPrompt = systemPromptComposer.Compose(new AISystemPromptContext
        {
            WorkspaceSystemPrompt = workspace.SystemPrompt,
            UserCustomContext = request.UserCustomContext,
            Tools = tools,
        });

        using Activity? activity = AIToolsActivitySource.Instance.StartActivity("ai.tools.orchestrate");
        activity?.SetTag("ai.workspace", workspaceName);
        activity?.SetTag("ai.guardrails.version", systemPrompt.Guardrails.Version);

        using IChatClient chatClient = await chatClientFactory.CreateAsync(workspaceName, cancellationToken).ConfigureAwait(false);

        List<ChatMessage> transcript = [new ChatMessage(ChatRole.System, systemPrompt.Text), .. request.Messages];
        List<AIToolInvocationOutcome> outcomes = [];
        long startTimestamp = timeProvider.GetTimestamp();

        long totalInput = 0;
        long totalOutput = 0;
        bool anyUsage = false;
        int iteration = 0;
        bool maxReached = false;
        string finalText = string.Empty;

        while (true)
        {
            iteration++;

            ChatResponse response = await chatClient
                .GetResponseAsync(transcript, chatOptions, cancellationToken)
                .ConfigureAwait(false);

            if (response.Usage is { } usage)
            {
                anyUsage = true;
                totalInput += usage.InputTokenCount ?? 0;
                totalOutput += usage.OutputTokenCount ?? 0;
            }

            transcript.AddRange(response.Messages);

            List<FunctionCallContent> calls = [.. response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()];

            if (calls.Count == 0)
            {
                finalText = response.Text ?? string.Empty;
                break;
            }

            if (iteration >= maxIterations)
            {
                maxReached = true;
                finalText = response.Text ?? string.Empty;
                LogMaxIterationsReached(maxIterations);
                break;
            }

            List<AIContent> results = new(calls.Count);
            foreach (FunctionCallContent call in calls)
            {
                (string content, bool succeeded, bool truncated) =
                    await ExecuteToolAsync(call, toolMap, options, cancellationToken).ConfigureAwait(false);

                results.Add(new FunctionResultContent(call.CallId, content));
                outcomes.Add(new AIToolInvocationOutcome
                {
                    ToolName = call.Name,
                    CallId = call.CallId,
                    Iteration = iteration,
                    Succeeded = succeeded,
                    Truncated = truncated,
                });

                metrics.RecordInvocation(tenantId, call.Name, succeeded ? "success" : "error");
                if (truncated)
                {
                    metrics.RecordTruncation(tenantId, call.Name);
                }
            }

            transcript.Add(new ChatMessage(ChatRole.Tool, results));
        }

        TimeSpan duration = timeProvider.GetElapsedTime(startTimestamp);
        metrics.RecordIterations(tenantId, iteration);

        if (anyUsage)
        {
            AIUsageRecord record = usageRecordFactory.Create(
                workspaceName,
                workspace.Provider,
                workspace.Model,
                (int)totalInput,
                (int)totalOutput,
                duration) with
            {
                PromptVersion = systemPrompt.Guardrails.Version,
            };

            await usageTracker.RecordAsync(record, cancellationToken).ConfigureAwait(false);
        }

        LogRunCompleted(workspaceName, iteration, outcomes.Count, maxReached);

        return new AIOrchestrationResult
        {
            Content = finalText,
            Messages = transcript,
            Iterations = iteration,
            MaxIterationsReached = maxReached,
            ToolInvocations = outcomes,
            InputTokens = anyUsage ? (int)totalInput : null,
            OutputTokens = anyUsage ? (int)totalOutput : null,
            Duration = duration,
        };
    }

    private async Task<(string Content, bool Succeeded, bool Truncated)> ExecuteToolAsync(
        FunctionCallContent call,
        Dictionary<string, IAITool> toolMap,
        GranitAIToolsOrchestrationOptions options,
        CancellationToken cancellationToken)
    {
        if (!toolMap.TryGetValue(call.Name, out IAITool? tool))
        {
            LogUnknownTool(call.Name);
            return ($"Error: no tool named '{call.Name}' is available.", false, false);
        }

        JsonElement arguments = SerializeArguments(call.Arguments);

        AIToolResult result;
        try
        {
            result = await tool
                .InvokeAsync(new AIToolInvocationContext { Arguments = arguments }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogToolFailed(ex, call.Name);
            return ($"Error: tool '{call.Name}' failed and could not complete the request.", false, false);
        }

        (string content, bool truncated) = Guard(result.Content, options.MaxToolResultCharacters);
        LogToolInvoked(call.Name, result.IsError, truncated);
        return (content, !result.IsError, truncated);
    }

    private static JsonElement SerializeArguments(IDictionary<string, object?>? arguments) =>
        arguments is null || arguments.Count == 0
            ? EmptyArguments
            : JsonSerializer.SerializeToElement(arguments, ArgumentSerializerOptions);

    private static (string Content, bool Truncated) Guard(string content, int maxCharacters)
    {
        if (maxCharacters <= 0 || content.Length <= maxCharacters)
        {
            return (content, false);
        }

        int dropped = content.Length - maxCharacters;
        return ($"{content[..maxCharacters]}\n\n[truncated {dropped} characters to fit the context window]", true);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "AI tool '{Tool}' invoked (isError={IsError}, truncated={Truncated}).")]
    private partial void LogToolInvoked(string tool, bool isError, bool truncated);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI agent requested unknown tool '{Tool}'.")]
    private partial void LogUnknownTool(string tool);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI tool '{Tool}' threw during invocation.")]
    private partial void LogToolFailed(Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI orchestration loop hit the iteration cap of {MaxIterations} before settling.")]
    private partial void LogMaxIterationsReached(int maxIterations);

    [LoggerMessage(Level = LogLevel.Information, Message = "AI orchestration run on workspace '{Workspace}' completed in {Iterations} iteration(s) with {ToolCalls} tool call(s) (maxReached={MaxReached}).")]
    private partial void LogRunCompleted(string workspace, int iterations, int toolCalls, bool maxReached);
}
