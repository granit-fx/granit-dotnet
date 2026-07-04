namespace Granit.TextExtraction;

/// <summary>
/// Provenance of a <see cref="TextExtractionResult"/>'s <see cref="TextExtractionResult.Content"/>.
/// Lets downstream consumers (indexers, summarisers, retrieval pipelines) apply trust rules
/// that match the extractor's failure mode — deterministic parsers produce ground truth,
/// heuristic / model-driven extractors do not.
/// </summary>
/// <remarks>
/// Re-feeding <see cref="ModelGenerated"/> content into another LLM prompt without an
/// isolating system message exposes the host to OWASP LLM01 (prompt injection): attacker
/// text embedded in an image or PDF flows into the next prompt verbatim.
/// </remarks>
public enum ExtractionConfidence
{
    /// <summary>
    /// Output of a deterministic byte-to-text parser (plain text, HTML DOM walk, OpenXml,
    /// PdfPig, Markdig). The same input always produces the same output; no opportunity for
    /// an attacker to hijack the extraction loop through the document contents.
    /// </summary>
    Deterministic,

    /// <summary>
    /// Output of a deterministic parser running on degraded input — typically a recovery
    /// path (e.g. PdfPig's raw-word fallback on a layout-analysis failure). The result is
    /// still produced by code, not a model, but ordering or punctuation may be wrong.
    /// </summary>
    Heuristic,

    /// <summary>
    /// Output produced by an LLM or VLM (vision OCR, future structured-extraction prompts).
    /// MUST be treated as untrusted by any consumer that re-prompts an LLM with it — wrap in
    /// an explicit envelope (<c>&lt;extracted&gt;...&lt;/extracted&gt;</c>) in the downstream
    /// system message, or strip before re-use.
    /// </summary>
    ModelGenerated,
}
