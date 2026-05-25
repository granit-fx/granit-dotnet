namespace Granit.TextExtraction;

/// <summary>
/// Result of an <see cref="ITextExtractor.ExtractAsync"/> call.
/// </summary>
/// <param name="Content">Extracted plain text. May be empty when the source contains no text.</param>
/// <param name="DetectedLanguage">
/// BCP-47 language tag detected from the content, or <c>null</c> when the extractor cannot
/// or does not perform language detection.
/// </param>
/// <param name="IsTruncated">
/// <c>true</c> when extraction stopped because the produced text reached the
/// <c>maxCharLength</c> cap. Consumers SHOULD treat truncated content as partial
/// (e.g. flag the indexed document as "partial").
/// </param>
/// <param name="CharCount">
/// Character count of <see cref="Content"/>. Always equals <c>Content.Length</c>; surfaced
/// separately for tracing/metrics tags so consumers don't pay a re-measurement cost.
/// </param>
/// <param name="ExtractorName">
/// The <see cref="ITextExtractor.Name"/> of the extractor that produced this result.
/// Recorded on metrics and spans for observability.
/// </param>
/// <param name="Confidence">
/// Provenance of <paramref name="Content"/>. <see cref="ExtractionConfidence.Deterministic"/>
/// for byte-to-text parsers, <see cref="ExtractionConfidence.Heuristic"/> for parser fallback
/// paths, <see cref="ExtractionConfidence.ModelGenerated"/> for LLM/VLM OCR.
/// Consumers re-prompting an LLM with this text MUST consult this field — model-generated
/// content carries the document author's instructions verbatim and is an OWASP LLM01 vector.
/// </param>
public sealed record TextExtractionResult(
    string Content,
    string? DetectedLanguage,
    bool IsTruncated,
    int CharCount,
    string ExtractorName,
    ExtractionConfidence Confidence);
