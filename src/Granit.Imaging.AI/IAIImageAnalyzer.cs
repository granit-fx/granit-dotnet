namespace Granit.Imaging.AI;

/// <summary>
/// Analyzes image content using multimodal LLM capabilities.
/// </summary>
/// <remarks>
/// Requires a multimodal AI provider (e.g. GPT-4o, Claude with vision) configured
/// in the <c>Granit.AI</c> workspace specified by <see cref="Options.ImagingAIOptions.WorkspaceName"/>.
/// The implementation sends image bytes as a <c>DataContent</c> message part alongside
/// a structured analysis prompt.
/// </remarks>
public interface IAIImageAnalyzer
{
    /// <summary>
    /// Analyzes the provided image data and returns structured analysis results.
    /// </summary>
    /// <param name="imageData">The raw binary content of the image.</param>
    /// <param name="contentType">The MIME type of the image (e.g. <c>image/png</c>, <c>image/jpeg</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="ImageAnalysis"/> containing description, detected objects, tags, and suggested alt text.</returns>
    Task<ImageAnalysis> AnalyzeAsync(
        ReadOnlyMemory<byte> imageData,
        string contentType,
        CancellationToken cancellationToken = default);
}
