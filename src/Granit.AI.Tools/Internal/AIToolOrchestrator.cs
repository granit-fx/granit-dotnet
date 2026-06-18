using System.Diagnostics;
using System.Runtime.CompilerServices;
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
/// Default <see cref="IAIToolOrchestrator"/>. Drives the agentic loop through
/// <see cref="FunctionInvokingChatClient"/> (the idiomatic Microsoft.Extensions.AI think → call →
/// execute → repeat loop), streaming assistant text and tool activity as it happens. Granit-specific
/// concerns — per-tool authorization, tool-result truncation, metrics, and the clarification
/// interrupt (via <see cref="FunctionInvocationContext.Terminate"/>) — are layered on through the
/// client's <see cref="FunctionInvokingChatClient.FunctionInvoker"/> hook (ADR-067/ADR-068).
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

    /// <summary>Bounded tag value for a model-issued call that names no registered tool.</summary>
    private const string UnknownToolTag = "<unknown>";

    public async Task<AIOrchestrationResult> RunAsync(
        AIOrchestrationRequest request,
        CancellationToken cancellationToken = default)
    {
        AIOrchestrationResult? result = null;
        await foreach (AIOrchestrationUpdate update in RunStreamingAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (update.Kind == AIOrchestrationUpdateKind.Completed)
            {
                result = update.Result;
            }
        }

        // RunStreamingAsync always terminates with a single Completed update.
        return result!;
    }

    public async IAsyncEnumerable<AIOrchestrationUpdate> RunStreamingAsync(
        AIOrchestrationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        GranitAIToolsOrchestrationOptions options = orchestrationOptions.Value;
        int maxIterations = Math.Max(1, options.MaxIterations);

        string workspaceName = request.WorkspaceName ?? aiOptions.Value.DefaultWorkspace;
        AIWorkspace workspace = await workspaceProvider.GetAsync(workspaceName, cancellationToken).ConfigureAwait(false)
            ?? throw new AIWorkspaceNotFoundException(workspaceName);
        string? tenantId = workspace.TenantId?.ToString();

        // Gate the candidate set to what the caller may use, so unauthorized tools are never declared
        // to the model nor executable this run (per-tool permission gate, ADR-067).
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

        IChatClient workspaceClient = await chatClientFactory.CreateAsync(workspaceName, cancellationToken).ConfigureAwait(false);

        // Run-local state captured by the FunctionInvoker; the agent client is fresh per run, so this
        // never bleeds across conversations.
        RunState state = new() { ToolMap = toolMap, TenantId = tenantId };

        // Count the underlying model round-trips (and whether the last one still wanted tools) directly,
        // rather than inferring iterations from streamed response ids — the function-invoking client
        // injects synthesized tool-result updates that would otherwise distort the count.
        ModelLoopChatClient loopClient = new(workspaceClient);

        // FunctionInvokingChatClient owns disposal of the inner client chain (DelegatingChatClient).
        using FunctionInvokingChatClient agentClient = new(loopClient)
        {
            MaximumIterationsPerRequest = maxIterations,
            // An unknown call yields an error result and the loop continues (the model self-corrects);
            // it never halts the loop.
            TerminateOnUnknownCalls = false,
            FunctionInvoker = (ctx, ct) => InvokeToolAsync(state, options, ctx, ct),
        };

        List<ChatMessage> messages = [new ChatMessage(ChatRole.System, systemPrompt.Text), .. request.Messages];

        long startTimestamp = timeProvider.GetTimestamp();
        long totalInput = 0;
        long totalOutput = 0;
        bool anyUsage = false;
        HashSet<string> announcedCalls = new(StringComparer.Ordinal);
        HashSet<string> announcedResults = new(StringComparer.Ordinal);
        List<ChatResponseUpdate> allUpdates = [];

        // Stream the agentic loop: text deltas and tool activity flow out as they happen; the updates
        // are also accumulated so the settled response (final text, usage, finish reason, transcript)
        // can be reassembled once the loop ends.
        await foreach (ChatResponseUpdate update in agentClient
            .GetStreamingResponseAsync(messages, chatOptions, cancellationToken)
            .ConfigureAwait(false))
        {
            allUpdates.Add(update);

            foreach (AIContent content in update.Contents)
            {
                switch (content)
                {
                    case TextContent text when !string.IsNullOrEmpty(text.Text):
                        yield return AIOrchestrationUpdate.Delta(text.Text);
                        break;

                    // Streamed function calls arrive fragmented; announce each call once (when a
                    // fragment first carries the name), keyed by call id.
                    case FunctionCallContent call when !string.IsNullOrEmpty(call.Name)
                        && call.CallId is { Length: > 0 } callId && announcedCalls.Add(callId):
                        yield return AIOrchestrationUpdate.ToolCall(call.Name, callId);
                        break;

                    case FunctionResultContent result when result.CallId is { Length: > 0 } resultId
                        && announcedResults.Add(resultId):
                        AIToolInvocationOutcome? outcome = state.OutcomesByCallId.GetValueOrDefault(resultId);
                        yield return AIOrchestrationUpdate.ToolResult(outcome?.ToolName ?? resultId, resultId, outcome?.Succeeded ?? true);
                        break;

                    case UsageContent usageContent:
                        anyUsage = true;
                        totalInput += usageContent.Details.InputTokenCount ?? 0;
                        totalOutput += usageContent.Details.OutputTokenCount ?? 0;
                        break;
                }
            }
        }

        var finalResponse = allUpdates.ToChatResponse();
        string finalText = finalResponse.Text ?? string.Empty;

        // One iteration per model round-trip.
        int iterations = Math.Max(1, loopClient.Calls);

        // The loop hit the cap iff it ran the maximum number of round-trips and the last one still
        // wanted tools (so the model intended to continue) — as opposed to settling on a final answer
        // or being halted by a tool interrupt.
        bool maxReached = loopClient.Calls >= maxIterations
            && loopClient.LastResponseHadToolCalls
            && state.Interrupt is null;

        TimeSpan duration = timeProvider.GetElapsedTime(startTimestamp);
        metrics.RecordIterations(tenantId, iterations);

        if (maxReached)
        {
            LogMaxIterationsReached(maxIterations);
        }

        if (state.Interrupt is not null)
        {
            LogInterrupted(state.Interrupt.Kind);
        }

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
                PromptTemplateName = request.InvokedPromptName,
                PromptTemplateVersion = request.InvokedPromptVersion,
            };

            // Stamp usage even if the caller's request was aborted mid-stream, but cap the write so a
            // stuck sink can't leak an orphaned task.
            using CancellationTokenSource usageCts = new(TimeSpan.FromSeconds(5));
            await usageTracker.RecordAsync(record, usageCts.Token).ConfigureAwait(false);
        }

        LogRunCompleted(workspaceName, iterations, state.Outcomes.Count, maxReached);

        yield return AIOrchestrationUpdate.Completed(new AIOrchestrationResult
        {
            Content = finalText,
            Messages = [.. finalResponse.Messages],
            Iterations = iterations,
            MaxIterationsReached = maxReached,
            ToolInvocations = state.Outcomes,
            InputTokens = anyUsage ? (int)totalInput : null,
            OutputTokens = anyUsage ? (int)totalOutput : null,
            Duration = duration,
            Interrupt = state.Interrupt,
        });
    }

    // The FunctionInvokingChatClient invocation hook: runs the resolved tool under the caller's gate,
    // guards the result size, records metrics and the audit outcome, and turns a tool-raised interrupt
    // into a graceful loop termination (the interrupt is surfaced on the Completed update).
    private async ValueTask<object?> InvokeToolAsync(
        RunState state,
        GranitAIToolsOrchestrationOptions options,
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        string name = context.CallContent.Name;
        string callId = context.CallContent.CallId;

        if (!state.ToolMap.TryGetValue(name, out IAITool? tool))
        {
            LogUnknownTool(name);
            state.Record(new AIToolInvocationOutcome
            {
                ToolName = name,
                CallId = callId,
                Iteration = context.Iteration + 1,
                Succeeded = false,
                Truncated = false,
            });
            metrics.RecordInvocation(state.TenantId, UnknownToolTag, "error");
            return $"Error: no tool named '{name}' is available.";
        }

        JsonElement arguments = SerializeArguments(context.Arguments);

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
            LogToolFailed(ex, name);
            state.Record(new AIToolInvocationOutcome
            {
                ToolName = name,
                CallId = callId,
                Iteration = context.Iteration + 1,
                Succeeded = false,
                Truncated = false,
            });
            metrics.RecordInvocation(state.TenantId, name, "error");
            return $"Error: tool '{name}' failed and could not complete the request.";
        }

        (string content, bool truncated) = Guard(result.Content, options.MaxToolResultCharacters);
        LogToolInvoked(name, result.IsError, truncated);

        state.Record(new AIToolInvocationOutcome
        {
            ToolName = name,
            CallId = callId,
            Iteration = context.Iteration + 1,
            Succeeded = !result.IsError,
            Truncated = truncated,
        });
        metrics.RecordInvocation(state.TenantId, name, result.IsError ? "error" : "success");
        if (truncated)
        {
            metrics.RecordTruncation(state.TenantId, name);
        }

        // A tool asked to halt the loop (e.g. a clarification request): surface its interrupt and stop
        // gracefully rather than feeding the result back to the model.
        if (result.Interrupt is not null)
        {
            state.Interrupt ??= result.Interrupt;
            context.Terminate = true;
        }

        return content;
    }

    // Per-run mutable state captured by the FunctionInvoker.
    private sealed class RunState
    {
        public required Dictionary<string, IAITool> ToolMap { get; init; }

        public required string? TenantId { get; init; }

        public List<AIToolInvocationOutcome> Outcomes { get; } = [];

        public Dictionary<string, AIToolInvocationOutcome> OutcomesByCallId { get; } = new(StringComparer.Ordinal);

        public AIToolInterrupt? Interrupt { get; set; }

        public void Record(AIToolInvocationOutcome outcome)
        {
            Outcomes.Add(outcome);
            OutcomesByCallId[outcome.CallId] = outcome;
        }
    }

    // Wraps the workspace client to count model round-trips and remember whether the last response
    // requested tools — the two signals the orchestrator needs to report Iterations / MaxIterationsReached
    // without having to mine the (synthesized-update-polluted) streamed response ids.
    private sealed class ModelLoopChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public int Calls { get; private set; }

        public bool LastResponseHadToolCalls { get; private set; }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++;
            bool hadToolCalls = false;
            await foreach (ChatResponseUpdate update in base
                .GetStreamingResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!hadToolCalls && update.Contents.OfType<FunctionCallContent>().Any())
                {
                    hadToolCalls = true;
                }

                yield return update;
            }

            LastResponseHadToolCalls = hadToolCalls;
        }
    }

    private static JsonElement SerializeArguments(IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        if (arguments is null)
        {
            return EmptyArguments;
        }

        // AIFunctionArguments carries pipeline state beyond the raw key/value pairs; copy only the
        // entries so serialization reflects the model-supplied arguments alone.
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> argument in arguments)
        {
            values[argument.Key] = argument.Value;
        }

        return values.Count == 0
            ? EmptyArguments
            : JsonSerializer.SerializeToElement(values, ArgumentSerializerOptions);
    }

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

    [LoggerMessage(Level = LogLevel.Information, Message = "AI orchestration loop interrupted by a '{Kind}' tool; awaiting caller.")]
    private partial void LogInterrupted(string kind);

    [LoggerMessage(Level = LogLevel.Information, Message = "AI orchestration run on workspace '{Workspace}' completed in {Iterations} iteration(s) with {ToolCalls} tool call(s) (maxReached={MaxReached}).")]
    private partial void LogRunCompleted(string workspace, int iterations, int toolCalls, bool maxReached);
}
