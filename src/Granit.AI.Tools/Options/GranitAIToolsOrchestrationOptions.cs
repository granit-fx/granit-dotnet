namespace Granit.AI.Tools.Options;

/// <summary>
/// Safety bounds for the agentic orchestration loop (ADR-067). Keeps an agent from running
/// away on cost or overflowing the context window.
/// </summary>
public sealed class GranitAIToolsOrchestrationOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "AI:Tools:Orchestration";

    /// <summary>
    /// Maximum number of think → call → execute iterations before the loop stops and returns
    /// whatever the model produced last. Must be at least 1. Default 8.
    /// </summary>
    public int MaxIterations { get; set; } = 8;

    /// <summary>
    /// Maximum number of characters of a single tool result fed back to the model. Longer
    /// results are truncated and the truncation is signalled to the model. Set to 0 to disable
    /// truncation (not recommended). Default 8000.
    /// </summary>
    public int MaxToolResultCharacters { get; set; } = 8000;
}
