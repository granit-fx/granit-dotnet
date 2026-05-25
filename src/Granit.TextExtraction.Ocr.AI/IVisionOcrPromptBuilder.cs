namespace Granit.TextExtraction.Ocr.AI;

/// <summary>
/// Builds the user-message prompt that accompanies the image when invoking the multimodal
/// model. Hosts customise the prompt by registering their own implementation BEFORE
/// calling <c>AddAIVisionOcrExtractor</c>.
/// </summary>
public interface IVisionOcrPromptBuilder
{
    /// <summary>
    /// Returns the user prompt for the given <paramref name="contentType"/> and
    /// <paramref name="maxCharLength"/>. The prompt should instruct the model to produce
    /// readable text only — table reformatting / Markdown structuring is allowed but
    /// summarisation is not.
    /// </summary>
    string BuildPrompt(string contentType, int maxCharLength);
}
