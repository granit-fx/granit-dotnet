namespace Granit.TextExtraction.Ocr.AI.Internal;

/// <summary>
/// Default <see cref="IVisionOcrPromptBuilder"/>. Produces a prompt that asks the model to
/// extract verbatim text and preserve tabular structure as GitHub-flavoured Markdown.
/// </summary>
internal sealed class DefaultVisionOcrPromptBuilder : IVisionOcrPromptBuilder
{
    public string BuildPrompt(string contentType, int maxCharLength) =>
        $"""
        Extract all readable text from this image verbatim. Preserve the original
        wording, line breaks, and bullet/numbering structure. Render any tables as
        GitHub-flavoured Markdown. Do NOT summarise, translate, paraphrase, or add
        commentary. If the image contains no readable text, return an empty response.
        Keep the response under {maxCharLength} characters.
        """;
}
