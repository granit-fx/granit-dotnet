namespace Granit.AI.Tools.Prompts;

/// <summary>
/// Inputs for composing the orchestrator system prompt.
/// </summary>
public sealed record AISystemPromptContext
{
    /// <summary>The workspace's own system prompt, or <see langword="null"/>.</summary>
    public string? WorkspaceSystemPrompt { get; init; }

    /// <summary>
    /// The user's custom context (Settings "U" scope, capped upstream), or <see langword="null"/>.
    /// Always layered below the guardrails so it can refine but never override them.
    /// </summary>
    public string? UserCustomContext { get; init; }

    /// <summary>
    /// The tools available this run. Those implementing <see cref="IAIToolInstructions"/>
    /// contribute their guidance to the prompt. May be <see langword="null"/> or empty.
    /// </summary>
    public IReadOnlyList<IAITool>? Tools { get; init; }
}

/// <summary>
/// The composed orchestrator system prompt plus the version of the guardrails it embeds, for
/// usage stamping.
/// </summary>
public sealed record AISystemPrompt
{
    /// <summary>The full system prompt text.</summary>
    public required string Text { get; init; }

    /// <summary>The guardrail prompt (and version) embedded at the top of <see cref="Text"/>.</summary>
    public required AIPromptVersion Guardrails { get; init; }
}

/// <summary>
/// Composes the orchestrator system prompt by layering, in strict precedence (ADR-067):
/// framework guardrails, then the workspace system prompt, then the user custom context, then
/// per-tool instructions. Tool <em>declarations</em> (the JSON schemas) are carried separately on
/// the request, not in this text.
/// </summary>
public interface IAISystemPromptComposer
{
    /// <summary>Composes the system prompt for the given context.</summary>
    AISystemPrompt Compose(AISystemPromptContext context);
}
