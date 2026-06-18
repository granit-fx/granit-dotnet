namespace Granit.AI.Tools;

/// <summary>Discriminates an <see cref="AIOrchestrationUpdate"/>.</summary>
public enum AIOrchestrationUpdateKind
{
    /// <summary>An incremental slice of assistant text streamed as the model produces it.</summary>
    Delta,

    /// <summary>A tool invocation started (emitted once per call id).</summary>
    ToolCall,

    /// <summary>A tool invocation finished (emitted once per call id).</summary>
    ToolResult,

    /// <summary>The terminal update carrying the settled <see cref="AIOrchestrationResult"/>.</summary>
    Completed,
}

/// <summary>
/// One update emitted by <see cref="IAIToolOrchestrator.RunStreamingAsync"/>. A stream interleaves
/// <see cref="AIOrchestrationUpdateKind.Delta"/> (assistant text as it arrives),
/// <see cref="AIOrchestrationUpdateKind.ToolCall"/> / <see cref="AIOrchestrationUpdateKind.ToolResult"/>
/// (tool activity, once per call id), and ends with exactly one terminal
/// <see cref="AIOrchestrationUpdateKind.Completed"/> update carrying the full
/// <see cref="AIOrchestrationResult"/> (final text, usage, interrupt, transcript).
/// </summary>
public sealed record AIOrchestrationUpdate
{
    /// <summary>Which kind of update this is.</summary>
    public required AIOrchestrationUpdateKind Kind { get; init; }

    /// <summary>The text slice, set when <see cref="Kind"/> is <see cref="AIOrchestrationUpdateKind.Delta"/>.</summary>
    public string? TextDelta { get; init; }

    /// <summary>The tool's wire name, set for <see cref="AIOrchestrationUpdateKind.ToolCall"/> / <see cref="AIOrchestrationUpdateKind.ToolResult"/>.</summary>
    public string? ToolName { get; init; }

    /// <summary>The model-issued call id correlating a tool call to its result.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Whether the tool succeeded, set for <see cref="AIOrchestrationUpdateKind.ToolResult"/>.</summary>
    public bool? Succeeded { get; init; }

    /// <summary>The settled result, set when <see cref="Kind"/> is <see cref="AIOrchestrationUpdateKind.Completed"/>.</summary>
    public AIOrchestrationResult? Result { get; init; }

    /// <summary>Creates a text-delta update.</summary>
    public static AIOrchestrationUpdate Delta(string text) =>
        new() { Kind = AIOrchestrationUpdateKind.Delta, TextDelta = text };

    /// <summary>Creates a tool-call-started update.</summary>
    public static AIOrchestrationUpdate ToolCall(string toolName, string callId) =>
        new() { Kind = AIOrchestrationUpdateKind.ToolCall, ToolName = toolName, ToolCallId = callId };

    /// <summary>Creates a tool-call-finished update.</summary>
    public static AIOrchestrationUpdate ToolResult(string toolName, string callId, bool succeeded) =>
        new() { Kind = AIOrchestrationUpdateKind.ToolResult, ToolName = toolName, ToolCallId = callId, Succeeded = succeeded };

    /// <summary>Creates the terminal completion update.</summary>
    public static AIOrchestrationUpdate Completed(AIOrchestrationResult result) =>
        new() { Kind = AIOrchestrationUpdateKind.Completed, Result = result };
}
