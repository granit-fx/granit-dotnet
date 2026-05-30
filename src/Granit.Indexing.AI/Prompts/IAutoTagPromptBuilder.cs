namespace Granit.Indexing.AI.Prompts;

/// <summary>
/// Builds the developer-controlled task instruction handed to the LLM for auto-tagging.
/// Overridable per-provider so hosts can tune the instruction, few-shot examples, or domain
/// glossaries without forking the auto-tagger.
/// </summary>
/// <remarks>
/// The untrusted document body is supplied and isolated separately by the
/// <see cref="Granit.AI.IStructuredCompletion"/> primitive (sanitized <c>&lt;data&gt;</c> block)
/// and the response is pinned by the JSON schema — so the builder only contributes the
/// instruction text (which lists the authoritative candidate set).
/// </remarks>
public interface IAutoTagPromptBuilder
{
    /// <summary>
    /// Builds the developer-controlled instruction. <paramref name="candidates"/> is the
    /// tenant's full tag universe (the model is told to pick a subset); <paramref name="maxTags"/>
    /// is the per-call cap.
    /// </summary>
    string BuildInstruction(IReadOnlyList<string> candidates, int maxTags);
}
