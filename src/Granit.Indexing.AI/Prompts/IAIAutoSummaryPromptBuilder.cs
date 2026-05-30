namespace Granit.Indexing.AI.Prompts;

/// <summary>
/// Builds the developer-controlled task instruction handed to the LLM for content
/// summarization. Overridable per-provider so hosts can tune the instruction, few-shot
/// examples, or target audiences without forking the summarizer.
/// </summary>
/// <remarks>
/// The untrusted content is supplied and isolated separately by the
/// <see cref="Granit.AI.IStructuredCompletion"/> primitive (sanitized <c>&lt;data&gt;</c> block)
/// and the response is pinned by the JSON schema — so the builder only contributes the
/// instruction text.
/// </remarks>
public interface IAIAutoSummaryPromptBuilder
{
    /// <summary>
    /// Builds the developer-controlled instruction. <paramref name="maxSummaryLength"/>
    /// communicates the expected response cap so the prompt can constrain the model upstream.
    /// </summary>
    string BuildInstruction(int maxSummaryLength);
}
