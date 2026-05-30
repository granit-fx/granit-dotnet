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
    /// Extracts (or generates) structured data of type <typeparamref name="TResult"/> from
    /// the supplied <paramref name="request"/>.
    /// </summary>
    /// <param name="request">
    /// The request carrying the developer-controlled <see cref="ExtractionRequest.Instruction"/>
    /// and the untrusted <see cref="ExtractionRequest.Content"/> / <see cref="ExtractionRequest.Context"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with typed data, confidence score, status, and model provenance.</returns>
    Task<ExtractionResult<TResult>> ExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts structured data from raw document text using the generic extraction instruction.
    /// </summary>
    /// <param name="content">Document text content (already extracted from PDF/image).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with typed data, confidence score, and status.</returns>
    /// <remarks>
    /// Convenience overload that delegates to <see cref="ExtractAsync(ExtractionRequest, CancellationToken)"/>
    /// with a default <see cref="ExtractionRequest"/>. Implementers only need to provide the
    /// <see cref="ExtractionRequest"/> overload.
    /// </remarks>
    Task<ExtractionResult<TResult>> ExtractAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ExtractAsync(new ExtractionRequest { Content = content }, cancellationToken);
    }
}
