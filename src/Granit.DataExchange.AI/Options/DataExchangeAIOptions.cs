namespace Granit.DataExchange.AI.Options;

/// <summary>
/// Configuration options for the AI-powered semantic mapping service.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:DataExchange</c> configuration section.
/// </remarks>
public sealed class DataExchangeAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:DataExchange";

    /// <summary>
    /// Name of the AI workspace to use for mapping suggestions.
    /// </summary>
    /// <remarks>
    /// Must correspond to a workspace configured in the <c>AI:Workspaces</c> section.
    /// Defaults to <c>"default"</c>.
    /// </remarks>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM call.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum confidence score (0.0 to 1.0) to accept a mapping suggestion.
    /// </summary>
    /// <remarks>
    /// Suggestions with a score below this threshold are discarded.
    /// </remarks>
    public double MinConfidenceScore { get; set; } = 0.6;
}
