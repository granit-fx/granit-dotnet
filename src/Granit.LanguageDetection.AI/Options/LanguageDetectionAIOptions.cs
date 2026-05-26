namespace Granit.LanguageDetection.AI.Options;

/// <summary>
/// Configuration options for <c>Granit.LanguageDetection.AI</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class LanguageDetectionAIOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "LanguageDetection:AI";

    /// <summary>
    /// AI workspace name resolved via <c>IAIChatClientFactory.CreateAsync(name)</c>.
    /// Default: <c>"default"</c>.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Hard cap on outbound LLM calls per tenant per hour. The 1001st call within the
    /// trailing hour returns <c>null</c> (graceful fall-through to the next provider in
    /// the composite chain) instead of throwing. Default: <c>1_000</c>.
    /// </summary>
    public int MaxAICallsPerHourPerTenant { get; set; } = 1_000;

    /// <summary>
    /// When <c>true</c>, content is routed through <c>IAIContentRedactor.Redact</c>
    /// before the LLM call. Default: <c>true</c>.
    /// </summary>
    public bool RedactPIIBeforeLLMCall { get; set; } = true;

    /// <summary>
    /// Maximum number of characters sampled from the head of the input before the
    /// LLM call. Trades cost for accuracy on long documents; the trigram fallback
    /// retains full coverage so this can stay modest. Default: <c>2_048</c>.
    /// </summary>
    public int MaxContentLength { get; set; } = 2_048;

    /// <summary>
    /// Per-call timeout. Above this the detector returns <c>null</c> so the composite
    /// falls through to the next provider rather than blocking the indexing path on a
    /// stalled LLM. Default: <c>10</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
