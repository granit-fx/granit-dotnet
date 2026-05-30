namespace Granit.LanguageDetection.AI.Prompts;

/// <summary>
/// Builds the developer-controlled task instruction handed to the language-detection model.
/// Overridable per-provider so hosts can tune the system instruction or few-shot examples
/// without touching the detector itself.
/// </summary>
/// <remarks>
/// The untrusted content is supplied and isolated separately by the
/// <see cref="Granit.AI.IStructuredCompletion"/> primitive (which wraps it in a sanitized
/// <c>&lt;data&gt;</c> block) and the output is pinned by the response schema — so the builder
/// only contributes the instruction text, not the message envelope.
/// </remarks>
public interface IAILanguageDetectionPromptBuilder
{
    /// <summary>
    /// Builds the developer-controlled detection instruction (never sanitized — it must be
    /// code- or template-controlled).
    /// </summary>
    string BuildInstruction();
}
