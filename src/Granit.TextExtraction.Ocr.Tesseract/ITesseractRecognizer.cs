namespace Granit.TextExtraction.Ocr.Tesseract;

/// <summary>
/// Runs Tesseract recognition on a single in-memory raster image and returns the decoded
/// text. Hosts can replace the default singleton (which serialises calls through one
/// locked <c>TesseractEngine</c>) with a pooled or GPU-accelerated implementation by
/// registering their own <see cref="ITesseractRecognizer"/> BEFORE calling
/// <c>AddTesseractOcrExtractor</c>.
/// </summary>
public interface ITesseractRecognizer
{
    /// <summary>
    /// Recognises the image bytes and returns the extracted text. Implementations MUST
    /// be safe to invoke concurrently from multiple callers — the default pins access
    /// via a lock around the underlying engine. Throws a provider-specific exception
    /// on failure; <see cref="TesseractOcrExtractor"/> converts those to a soft-skip
    /// result.
    /// </summary>
    Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
