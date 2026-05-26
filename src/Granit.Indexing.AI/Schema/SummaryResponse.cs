namespace Granit.Indexing.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI summarizer. The LLM is constrained via
/// <c>ChatResponseFormat.ForJsonSchema&lt;SummaryResponse&gt;()</c> so it cannot return
/// free-form text — out-of-schema outputs are rejected at the parsing layer and bumped
/// to the <c>granit.indexing.ai.summarizer.injection_attempt</c> metric.
/// </summary>
public sealed class SummaryResponse
{
    /// <summary>
    /// The generated summary. Empty when the model decided the document had no
    /// summarisable content; truncated when it exceeds the configured cap.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}
