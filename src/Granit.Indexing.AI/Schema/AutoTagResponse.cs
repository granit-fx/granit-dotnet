namespace Granit.Indexing.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI auto-tagger. The LLM is constrained via
/// <c>ChatResponseFormat.ForJsonSchema&lt;AutoTagResponse&gt;()</c> so it cannot return
/// free-form text — out-of-schema outputs are rejected at parse time and bumped to the
/// <c>granit.indexing.ai.autotag.injection_attempt</c> metric.
/// </summary>
public sealed class AutoTagResponse
{
    /// <summary>
    /// Suggested tag strings drawn from the candidate list. Server-side
    /// intersection (in <c>AIAutoTagger</c>) drops any value that is NOT in the
    /// candidate universe, so the LLM cannot inject novel tags into the consumer's
    /// taxonomy — a hostile prompt that proposes "delete-everything" gets discarded
    /// silently. Empty array means "no confident match".
    /// </summary>
    public string[] Tags { get; set; } = [];
}
