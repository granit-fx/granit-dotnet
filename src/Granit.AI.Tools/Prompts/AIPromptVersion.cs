namespace Granit.AI.Tools.Prompts;

/// <summary>
/// A code-first, versioned prompt fragment (ADR-067). The <see cref="Version"/> is stamped into
/// the usage record so an interaction can be traced back to the exact instruction text that
/// produced it, without persisting the content itself.
/// </summary>
public sealed record AIPromptVersion
{
    /// <summary>Stable identifier for this prompt (e.g. <c>framework.guardrails</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The prompt's version (e.g. <c>1.0.0</c>). Bumped whenever the content changes.</summary>
    public required string Version { get; init; }

    /// <summary>The prompt text.</summary>
    public required string Content { get; init; }
}
