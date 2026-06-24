using System.ComponentModel.DataAnnotations;

namespace Granit.Validation.AI.Options;

/// <summary>
/// Configuration options for AI-powered content moderation.
/// </summary>
/// <remarks>
/// Bound to the <c>Validation:AI</c> configuration section. Validated via
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> so misconfiguration fails fast at boot.
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
    [Required]
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Timeout in seconds for the LLM moderation call.
    /// Validation should not block the request, so keep this low.
    /// </summary>
    [Range(1, 30, ErrorMessage = "TimeoutSeconds must be between 1 and 30.")]
    public int TimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// Minimum severity score (0.0–1.0) for a flag to be included in the result.
    /// Flags below this threshold are silently discarded.
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "SeverityThreshold must be between 0.0 and 1.0.")]
    public double SeverityThreshold { get; set; } = 0.5;
}
