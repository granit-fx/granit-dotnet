namespace Granit.LanguageDetection.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI language detector. The LLM is constrained
/// via <c>ChatResponseFormat.ForJsonSchema&lt;LanguageDetectionResponse&gt;()</c> so it
/// cannot return free-form text — out-of-schema outputs are rejected at the parsing
/// layer and bumped to the <c>granit.language_detection.ai.injection_attempt</c> metric.
/// </summary>
public sealed class LanguageDetectionResponse
{
    /// <summary>
    /// ISO 639-1 alpha-2 code, or empty string when the model could not classify the
    /// sample. The detector validates the value matches <c>^[a-z]{2}$</c> before
    /// returning it; non-conforming responses are treated as injection attempts.
    /// </summary>
    public string Language { get; set; } = string.Empty;
}
