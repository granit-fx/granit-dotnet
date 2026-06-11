namespace Granit.Validation.AI.Options;

/// <summary>
/// Configuration options for AI-powered content moderation.
/// </summary>
/// <remarks>
/// Bound to the <c>Validation:AI</c> configuration section.
/// </remarks>
public sealed class ValidationAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Validation:AI";

    /// <summary>
    /// Name of the AI workspace used for content moderation.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM moderation call.
    /// Validation should not block the request, so keep this low.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// Minimum severity score (0.0–1.0) for a flag to be included in the result.
    /// Flags below this threshold are silently discarded.
    /// </summary>
    public double SeverityThreshold { get; set; } = 0.5;
}
