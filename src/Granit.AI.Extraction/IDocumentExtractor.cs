namespace Granit.AI.Extraction;

/// <summary>
/// Extracts structured data of type <typeparamref name="TResult"/> from document text using LLM.
/// </summary>
/// <typeparam name="TResult">The type of the structured data to extract.</typeparam>
/// <remarks>
/// Takes <c>string</c> content (not <c>Stream</c>) because document-to-text conversion
/// (PDF parsing, OCR) is a separate concern. This keeps the extraction module focused
/// on LLM-based structured extraction.
/// </remarks>
public interface IDocumentExtractor<TResult> where TResult : class
{
    /// <summary>
    /// Extracts structured data from a document.
    /// </summary>
    /// <param name="content">Document text content (already extracted from PDF/image).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with typed data, confidence score, and status.</returns>
    Task<ExtractionResult<TResult>> ExtractAsync(
        string content,
        CancellationToken cancellationToken = default);
}
