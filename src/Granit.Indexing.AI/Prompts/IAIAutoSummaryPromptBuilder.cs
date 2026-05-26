using Microsoft.Extensions.AI;

namespace Granit.Indexing.AI.Prompts;

/// <summary>
/// Builds the chat-message sequence handed to the LLM for content summarization.
/// Overridable per-provider so hosts can tune system instructions, few-shot examples
/// or target audiences without forking the summarizer.
/// </summary>
public interface IAIAutoSummaryPromptBuilder
{
    /// <summary>
    /// Builds the (system + user) message pair. <paramref name="content"/> is the
    /// (possibly redacted, possibly truncated) text the framework wants summarized.
    /// <paramref name="maxSummaryLength"/> communicates the expected response cap so
    /// the prompt can constrain the model upstream.
    /// </summary>
    IReadOnlyList<ChatMessage> Build(string content, int maxSummaryLength);
}
