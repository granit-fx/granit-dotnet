namespace Granit.AI.Extraction;

/// <summary>
/// Factory methods for creating <see cref="ExtractionResult{TResult}"/> instances.
/// </summary>
public static class ExtractionResult
{
    /// <summary>
    /// Creates a successful extraction result.
    /// </summary>
    /// <typeparam name="TResult">The type of the extracted data.</typeparam>
    /// <param name="data">The extracted data.</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <param name="modelId">Identifier of the model that produced the result, if known.</param>
    /// <returns>A successful <see cref="ExtractionResult{TResult}"/>.</returns>
    public static ExtractionResult<TResult> Success<TResult>(TResult data, double confidence, IReadOnlyList<string>? warnings = null, string? modelId = null)
        where TResult : class =>
        new()
        {
            Status = ExtractionStatus.Succeeded,
            Data = data,
            ConfidenceScore = confidence,
            Warnings = warnings ?? [],
            ModelId = modelId,
        };

    /// <summary>
    /// Creates a failed extraction result.
    /// </summary>
    /// <typeparam name="TResult">The type of the extracted data.</typeparam>
    /// <param name="errorMessage">Description of the failure.</param>
    /// <param name="modelId">Identifier of the model that produced the result, if known.</param>
    /// <returns>A failed <see cref="ExtractionResult{TResult}"/>.</returns>
    public static ExtractionResult<TResult> Failed<TResult>(string errorMessage, string? modelId = null)
        where TResult : class =>
        new()
        {
            Status = ExtractionStatus.Failed,
            ErrorMessage = errorMessage,
            ModelId = modelId,
        };

    /// <summary>
    /// Creates an extraction result that needs manual review due to low confidence or warnings.
    /// </summary>
    /// <typeparam name="TResult">The type of the extracted data.</typeparam>
    /// <param name="data">The extracted data.</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0.</param>
    /// <param name="warnings">Warnings explaining why review is needed.</param>
    /// <param name="modelId">Identifier of the model that produced the result, if known.</param>
    /// <returns>A <see cref="ExtractionResult{TResult}"/> with <see cref="ExtractionStatus.NeedsReview"/> status.</returns>
    public static ExtractionResult<TResult> NeedsReview<TResult>(TResult data, double confidence, IReadOnlyList<string> warnings, string? modelId = null)
        where TResult : class =>
        new()
        {
            Status = ExtractionStatus.NeedsReview,
            Data = data,
            ConfidenceScore = confidence,
            Warnings = warnings,
            ModelId = modelId,
        };
}

/// <summary>
/// Result of a document extraction operation containing typed data, confidence score, and status.
/// </summary>
/// <typeparam name="TResult">The type of the extracted data.</typeparam>
public sealed record ExtractionResult<TResult> where TResult : class
{
    /// <summary>
    /// Status of the extraction operation.
    /// </summary>
    public required ExtractionStatus Status { get; init; }

    /// <summary>
    /// Extracted data. <c>null</c> when <see cref="Status"/> is <see cref="ExtractionStatus.Failed"/>.
    /// </summary>
    public TResult? Data { get; init; }

    /// <summary>
    /// Confidence score between 0.0 and 1.0, if available.
    /// </summary>
    public double? ConfidenceScore { get; init; }

    /// <summary>
    /// Error message when <see cref="Status"/> is <see cref="ExtractionStatus.Failed"/>.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Warnings produced during extraction (e.g. missing optional fields, low-confidence fields).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Identifier of the model that produced this result, as reported by the provider.
    /// <c>null</c> when the provider did not surface a model identifier (e.g. early failures).
    /// </summary>
    public string? ModelId { get; init; }
}
