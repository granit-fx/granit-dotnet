namespace Granit.Imaging.AI;

/// <summary>The text read from an image, plus the workspace that produced it.</summary>
/// <param name="Text">The extracted text (may be empty when the image contains none).</param>
/// <param name="Workspace">The vision-capable workspace that performed the extraction.</param>
public sealed record ImageTextExtractionResult(string Text, string Workspace);

/// <summary>
/// Reads text out of an image using a vision-capable AI workspace, decoupled from the chat
/// workspace (ADR-067). Routing resolves a Vision-capable workspace via the capability resolver;
/// the call stamps its own usage record.
/// </summary>
public interface IImageTextExtractor
{
    /// <summary>
    /// Extracts text from the image, or returns <see langword="null"/> when no vision-capable
    /// workspace is configured (default-off — the caller degrades gracefully).
    /// </summary>
    /// <param name="imageData">The raw image bytes.</param>
    /// <param name="contentType">The image MIME type (e.g. <c>image/png</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ImageTextExtractionResult?> ExtractTextAsync(
        ReadOnlyMemory<byte> imageData,
        string contentType,
        CancellationToken cancellationToken = default);
}
