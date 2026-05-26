using Microsoft.Extensions.AI;

namespace Granit.Indexing.AI.Prompts;

/// <summary>
/// Builds the chat-message sequence handed to the LLM for auto-tagging. Overridable
/// per-provider so hosts can tune system instructions, few-shot examples, or domain
/// glossaries without forking the auto-tagger.
/// </summary>
public interface IAutoTagPromptBuilder
{
    /// <summary>
    /// Builds the (system + user) message pair. <paramref name="content"/> is the
    /// (possibly redacted, possibly truncated) document body. <paramref name="candidates"/>
    /// is the tenant's full tag universe — the model is instructed to pick a subset.
    /// <paramref name="maxTags"/> communicates the per-call cap so the prompt can
    /// constrain the model upstream.
    /// </summary>
    IReadOnlyList<ChatMessage> Build(
        string content,
        IReadOnlyList<string> candidates,
        int maxTags);
}
