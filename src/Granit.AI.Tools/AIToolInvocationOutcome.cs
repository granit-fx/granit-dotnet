namespace Granit.AI.Tools;

/// <summary>
/// Audit entry for a single tool call executed during an orchestration run.
/// </summary>
public sealed record AIToolInvocationOutcome
{
    /// <summary>The tool name the model requested.</summary>
    public required string ToolName { get; init; }

    /// <summary>The provider call id correlating the request and its result.</summary>
    public required string CallId { get; init; }

    /// <summary>The loop iteration (1-based) on which the call ran.</summary>
    public required int Iteration { get; init; }

    /// <summary>
    /// <see langword="true"/> when the tool returned a non-error result. A missing tool or a
    /// thrown exception is recorded as <see langword="false"/>.
    /// </summary>
    public required bool Succeeded { get; init; }

    /// <summary><see langword="true"/> when the result was truncated to fit the context window.</summary>
    public required bool Truncated { get; init; }
}
