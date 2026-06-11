namespace Granit.Imaging.AI.Options;

/// <summary>
/// Configuration options for AI-powered image analysis.
/// </summary>
/// <remarks>
/// Bound to the <c>Imaging:AI</c> configuration section.
/// </remarks>
public sealed class ImagingAIOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Imaging:AI";

    /// <summary>
    /// Name of the AI workspace to use for image analysis.
    /// </summary>
    /// <remarks>
    /// The workspace must be configured with a multimodal model that supports
    /// image inputs (e.g. GPT-4o, Claude with vision).
    /// When <c>null</c>, the default workspace from <c>GranitAIOptions</c> is used.
    /// </remarks>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// Timeout in seconds for a single image analysis request.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;
}
