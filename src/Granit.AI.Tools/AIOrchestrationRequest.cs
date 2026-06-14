using Microsoft.Extensions.AI;

namespace Granit.AI.Tools;

/// <summary>
/// Input to <see cref="IAIToolOrchestrator.RunAsync"/>: the conversation so far and the tools
/// the agent may call for this run.
/// </summary>
public sealed record AIOrchestrationRequest
{
    /// <summary>
    /// The chat-capable workspace to drive the loop, or <see langword="null"/> to use the
    /// configured default workspace.
    /// </summary>
    public string? WorkspaceName { get; init; }

    /// <summary>
    /// The conversation so far (system / user / assistant / tool messages). The loop appends
    /// assistant and tool messages to a copy of this list as it runs.
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// The tools the agent may call this run, or <see langword="null"/> to expose every tool in
    /// the <see cref="IAIToolRegistry"/>. Pass a curated subset to narrow the agent's reach.
    /// </summary>
    public IReadOnlyList<IAITool>? Tools { get; init; }

    /// <summary>
    /// Optional per-user custom context (Settings "U" scope, capped upstream) layered into the
    /// system prompt below the framework guardrails — it can refine behaviour but never override
    /// the guardrails.
    /// </summary>
    public string? UserCustomContext { get; init; }

    /// <summary>
    /// Name of the catalogue prompt template that drove this run (ADR-067 badge resolution), or
    /// <see langword="null"/>. Stamped into the usage record for auditability; does not affect the loop.
    /// </summary>
    public string? InvokedPromptName { get; init; }

    /// <summary>
    /// Revision of the catalogue prompt template named by <see cref="InvokedPromptName"/>, or
    /// <see langword="null"/>. Stamped into the usage record alongside the name.
    /// </summary>
    public int? InvokedPromptVersion { get; init; }
}
