using Microsoft.Extensions.AI;

namespace Granit.AI.Tools;

/// <summary>
/// Outcome of an <see cref="IAIToolOrchestrator.RunAsync"/> run.
/// </summary>
public sealed record AIOrchestrationResult
{
    /// <summary>The final assistant text after the loop settled (no further tool calls).</summary>
    public required string Content { get; init; }

    /// <summary>
    /// The full transcript including the assistant tool-call messages and the tool-result
    /// messages produced during the loop.
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>The number of model round-trips the loop made.</summary>
    public required int Iterations { get; init; }

    /// <summary>
    /// <see langword="true"/> when the loop stopped because it hit
    /// <see cref="Options.GranitAIToolsOrchestrationOptions.MaxIterations"/> while the model was
    /// still requesting tools, rather than settling on a final answer.
    /// </summary>
    public required bool MaxIterationsReached { get; init; }

    /// <summary>The audit trail of every tool call executed during the run, in order.</summary>
    public required IReadOnlyList<AIToolInvocationOutcome> ToolInvocations { get; init; }

    /// <summary>Total input tokens across all iterations, or <see langword="null"/> if unreported.</summary>
    public int? InputTokens { get; init; }

    /// <summary>Total output tokens across all iterations, or <see langword="null"/> if unreported.</summary>
    public int? OutputTokens { get; init; }

    /// <summary>Wall-clock duration of the whole run.</summary>
    public TimeSpan Duration { get; init; }
}
