namespace Granit.Timeline.AI.Options;

/// <summary>
/// Configuration options for AI-powered timeline analysis.
/// </summary>
/// <remarks>
/// Bound to the <c>Timeline:AI</c> configuration section.
/// </remarks>
public sealed class TimelineAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Timeline:AI";

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
    /// Maximum number of timeline entries fed to the summarizer LLM. Capped low
    /// because summarization is token-budget bound — beyond ~200 entries the
    /// LLM context window becomes the bottleneck, not the input completeness.
    /// </summary>
    public int SummarizerMaxEntries { get; set; } = 200;

    /// <summary>
    /// Maximum number of timeline entries fed to the anomaly-detection LLM.
    /// Higher than the summarizer cap because anomaly signal improves with
    /// volume — needles in haystacks. Stays well below
    /// <c>TimelineOptions.MaxPage × pageSize</c> so the federated reader cap
    /// is not hit.
    /// </summary>
    public int AnomalyDetectorMaxEntries { get; set; } = 500;

    /// <summary>
    /// Maximum number of concurrent LLM requests for timeline AI operations per scope.
    /// Prevents denial-of-wallet attacks via unbounded parallel LLM calls.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 3;
}
