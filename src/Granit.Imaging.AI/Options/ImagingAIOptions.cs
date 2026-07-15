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
    /// <remarks>
    /// The analysis path also runs under the structured-completion primitive's own
    /// <c>StructuredCompletionOptions.TimeoutSeconds</c> — the lower of the two wins.
    /// </remarks>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum image size in bytes accepted by the AI analysis/extraction paths.
    /// Default: 10 MB. Set to <c>0</c> to disable the guard.
    /// </summary>
    /// <remarks>
    /// This is a fail-fast guard, not an allocation guard: the image reaches this module
    /// already materialized in memory (the byte source — HTTP limits, BlobStorage
    /// <c>MaxInputBytes</c> — owns that). Failing here avoids the ~1.33x base64 expansion,
    /// the provider serialization, and a wasted round-trip to the model.
    /// </remarks>
    public long MaxImageBytes { get; set; } = 10 * 1024 * 1024;
}
