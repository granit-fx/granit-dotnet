namespace Granit.Indexing.AI.Options;

/// <summary>
/// Configuration options for <c>Granit.Indexing.AI</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class IndexingAIOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Indexing:AI";

    /// <summary>
    /// AI workspace name resolved via <c>IAIChatClientFactory.CreateAsync(name)</c>.
    /// Default: <c>"default"</c>.
    /// </summary>
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Hard cap on outbound LLM calls per tenant per hour. Calls above the cap return
    /// <c>null</c> (graceful skip — caller persists the entry without a summary)
    /// instead of throwing. Default: <c>1_000</c>.
    /// </summary>
    public int MaxAICallsPerHourPerTenant { get; set; } = 1_000;

    /// <summary>
    /// When <c>true</c>, content is routed through <c>IAIContentRedactor.Redact</c>
    /// before the LLM call. Default: <c>true</c>.
    /// </summary>
    public bool RedactPIIBeforeLLMCall { get; set; } = true;

    /// <summary>
    /// Maximum number of characters sampled from the head of the input before the
    /// LLM call. Caps cost on very long documents — the summarizer is meant to
    /// produce a SERP snippet, not exhaustively cover the body. Default: <c>8_192</c>.
    /// </summary>
    public int MaxContentLength { get; set; } = 8_192;

    /// <summary>
    /// Hard ceiling on the summary length returned to the caller. Responses above
    /// the cap are truncated and tagged with a metric so the host can review prompt
    /// adherence. Default: <c>500</c>.
    /// </summary>
    public int MaxSummaryLength { get; set; } = 500;

    /// <summary>
    /// Per-call timeout. Above this the summarizer returns <c>null</c> so the
    /// indexing pipeline keeps flowing on a stalled LLM. Default: <c>20</c>.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Hard cap on outbound LLM calls per tenant per hour dedicated to the
    /// auto-tagger. Distinct from <see cref="MaxAICallsPerHourPerTenant"/> so the
    /// (cheaper, more frequent) summarizer budget doesn't starve auto-tagging on
    /// the same tenant. Calls above the cap return an empty tag list (graceful
    /// skip) instead of throwing. Default: <c>500</c>.
    /// </summary>
    public int MaxAutoTagCallsPerHourPerTenant { get; set; } = 500;

    /// <summary>
    /// Maximum number of characters sampled from the head of the input before the
    /// auto-tagger LLM call. Smaller than the summarizer cap — auto-tagging needs
    /// the document's gist, not the full body — to keep token cost in check.
    /// Default: <c>4_096</c>.
    /// </summary>
    public int MaxAutoTagContentLength { get; set; } = 4_096;

    /// <summary>
    /// Hard ceiling on the number of tags returned per call regardless of what
    /// the LLM proposes. Mirrors the <c>maxTags</c> caller parameter — whichever
    /// is smaller wins. Default: <c>10</c>.
    /// </summary>
    public int MaxAutoTagsReturned { get; set; } = 10;
}
