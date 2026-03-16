namespace Granit.BlobStorage.AI;

/// <summary>
/// Classifies a blob by filename and content type using an LLM.
/// </summary>
/// <remarks>
/// Use this interface to request classification outside the <see cref="IBlobValidator"/> pipeline.
/// The implementation is also registered as an <see cref="IBlobValidator"/> (Order = 100) for
/// automatic classification during upload validation.
/// </remarks>
public interface IAIBlobClassifier
{
    /// <summary>
    /// Classifies a file based on its name and declared content type.
    /// </summary>
    /// <param name="fileName">Original filename (e.g. "invoice-2026-03.pdf").</param>
    /// <param name="contentType">Declared MIME type (e.g. "application/pdf").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Classification result with category, confidence, tags, and PII detection.</returns>
    Task<BlobClassification> ClassifyAsync(
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
