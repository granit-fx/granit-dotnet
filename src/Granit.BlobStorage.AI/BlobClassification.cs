namespace Granit.BlobStorage.AI;

/// <summary>
/// Result of AI-based blob classification.
/// </summary>
/// <param name="Category">File category determined by the LLM (e.g. "invoice", "identity_document", "photo", "contract").</param>
/// <param name="Confidence">Confidence score between 0.0 and 1.0.</param>
/// <param name="DetectedTags">Tags detected by the LLM (e.g. "financial", "legal", "personal").</param>
/// <param name="ContainsPiiInFileName">Whether the original filename appears to contain personally identifiable information.</param>
public sealed record BlobClassification(
    string Category,
    double Confidence,
    IReadOnlyList<string> DetectedTags,
    bool ContainsPiiInFileName);
