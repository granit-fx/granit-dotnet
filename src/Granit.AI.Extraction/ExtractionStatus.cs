namespace Granit.AI.Extraction;

/// <summary>
/// Status of a document extraction operation.
/// </summary>
public enum ExtractionStatus
{
    /// <summary>
    /// Extraction completed successfully with high confidence.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Extraction completed but with low confidence or validation warnings — manual review recommended.
    /// </summary>
    NeedsReview,

    /// <summary>
    /// Extraction failed — no usable data could be extracted.
    /// </summary>
    Failed,
}
