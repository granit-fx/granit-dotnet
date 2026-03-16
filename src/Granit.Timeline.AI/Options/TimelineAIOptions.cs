namespace Granit.Timeline.AI.Options;

/// <summary>
/// Configuration options for AI-powered timeline analysis.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Timeline</c> configuration section.
/// </remarks>
public sealed class TimelineAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:Timeline";

    /// <summary>
    /// Name of the AI workspace to use for timeline analysis.
    /// When <c>null</c>, the default workspace is used.
    /// </summary>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// Timeout in seconds for LLM requests.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum number of timeline entries to include in a single LLM analysis request.
    /// Older entries are truncated when the stream exceeds this limit.
    /// </summary>
    public int MaxEntriesToAnalyze { get; set; } = 100;
}
