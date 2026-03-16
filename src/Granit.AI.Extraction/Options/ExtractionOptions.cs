namespace Granit.AI.Extraction.Options;

/// <summary>
/// Configuration options for AI document extraction.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Extraction</c> configuration section.
/// </remarks>
public sealed class ExtractionOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:Extraction";

    /// <summary>
    /// AI workspace name to use for extraction. Defaults to <c>"default"</c>.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Confidence threshold below which the extraction status is set to
    /// <see cref="ExtractionStatus.NeedsReview"/>. Defaults to <c>0.7</c>.
    /// </summary>
    public double ReviewThreshold { get; set; } = 0.7;

    /// <summary>
    /// Maximum time in seconds to wait for an extraction to complete. Defaults to <c>30</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
