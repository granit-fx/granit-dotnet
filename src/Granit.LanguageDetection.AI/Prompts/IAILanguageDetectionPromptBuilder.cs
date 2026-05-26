using Microsoft.Extensions.AI;

namespace Granit.LanguageDetection.AI.Prompts;

/// <summary>
/// Builds the chat-message sequence handed to the LLM for language detection.
/// Overridable per-provider so hosts can tune system instructions or few-shot
/// examples without touching the detector itself.
/// </summary>
public interface IAILanguageDetectionPromptBuilder
{
    /// <summary>
    /// Builds the (system + user) message pair. <paramref name="content"/> is the
    /// (possibly redacted, possibly truncated) text the framework wants classified.
    /// </summary>
    IReadOnlyList<ChatMessage> Build(string content);
}
