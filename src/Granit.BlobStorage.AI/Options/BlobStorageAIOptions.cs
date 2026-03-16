namespace Granit.BlobStorage.AI.Options;

/// <summary>
/// Configuration options for the AI blob classifier.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:BlobStorage</c> configuration section.
/// </remarks>
public sealed class BlobStorageAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:BlobStorage";

    /// <summary>
    /// Name of the AI workspace to use for blob classification.
    /// When <c>null</c>, the default workspace is used.
    /// </summary>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// Timeout in seconds for the LLM classification request.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether to detect PII in filenames and flag them during validation.
    /// </summary>
    /// <remarks>
    /// When enabled, filenames containing patterns that resemble personal identifiers
    /// (social security numbers, email addresses, phone numbers, etc.) will cause
    /// the validation step to return a failure result.
    /// </remarks>
    public bool EnablePiiDetection { get; set; } = true;
}
