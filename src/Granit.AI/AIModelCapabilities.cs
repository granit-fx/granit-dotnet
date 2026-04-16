namespace Granit.AI;

/// <summary>
/// Describes the capabilities supported by an AI model.
/// </summary>
/// <remarks>
/// Core capabilities are strongly typed boolean properties with sensible defaults.
/// Provider-specific features (web search, code interpreter, citations, etc.) are
/// exposed via the <see cref="Extensions"/> set — extensible without breaking changes.
/// </remarks>
public sealed record AIModelCapabilities
{
    /// <summary>Whether the model supports chat completions.</summary>
    public bool Chat { get; init; } = true;

    /// <summary>Whether the model supports embedding generation.</summary>
    public bool Embeddings { get; init; }

    /// <summary>Whether the model supports image input (vision).</summary>
    public bool Vision { get; init; }

    /// <summary>Whether the model can generate images.</summary>
    public bool ImageGeneration { get; init; }

    /// <summary>Whether the model supports audio input or output.</summary>
    public bool Audio { get; init; }

    /// <summary>Whether the model supports tool/function calling.</summary>
    public bool ToolUse { get; init; }

    /// <summary>Whether the model supports streaming responses.</summary>
    public bool Streaming { get; init; } = true;

    /// <summary>Whether the model supports structured output (JSON mode).</summary>
    public bool StructuredOutput { get; init; }

    /// <summary>
    /// Provider-specific capability extensions not covered by core properties.
    /// Use <see cref="WellKnownAICapabilities"/> constants for well-known extensions.
    /// </summary>
    public IReadOnlySet<string> Extensions { get; init; } = new HashSet<string>();
}
