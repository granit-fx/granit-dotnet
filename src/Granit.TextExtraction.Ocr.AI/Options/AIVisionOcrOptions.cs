namespace Granit.TextExtraction.Ocr.AI.Options;

/// <summary>
/// Configuration options for the AI vision OCR extractor. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class AIVisionOcrOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TextExtraction:Ocr:AI";

    /// <summary>
    /// Name of the Granit.AI workspace whose <see cref="Microsoft.Extensions.AI.IChatClient"/>
    /// the extractor uses. When <c>null</c> the default workspace from
    /// <c>GranitAIOptions.DefaultWorkspace</c> is used. The named workspace MUST point at a
    /// model with multimodal / vision capability.
    /// </summary>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// MIME types the extractor will claim via <see cref="ITextExtractor.CanHandle"/>.
    /// Defaults to the raster formats reliably supported by most VLM providers.
    /// </summary>
    public IList<string> AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/tiff",
    ];
}
