using System.ComponentModel.DataAnnotations;

namespace Granit.AI.Extraction.Options;

/// <summary>
/// Configuration options for AI document extraction.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Extraction</c> configuration section and validated at startup via
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> so an out-of-range value aborts
/// host boot instead of silently degrading every extraction.
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
    [Required(AllowEmptyStrings = false)]
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Confidence threshold below which the extraction status is set to
    /// <see cref="ExtractionStatus.NeedsReview"/>. Defaults to <c>0.7</c>.
    /// </summary>
    [Range(0.0, 1.0)]
    public double ReviewThreshold { get; set; } = 0.7;

    /// <summary>
    /// Maximum time in seconds to wait for an extraction to complete. Defaults to <c>30</c>.
    /// </summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 30;
}
