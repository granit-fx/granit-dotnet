using Granit.TextExtraction.Options;

namespace Granit.TextExtraction;

/// <summary>
/// Contract implemented by every concrete text extractor (PDF, Office, HTML, OCR, ...).
/// </summary>
/// <remarks>
/// Implementations MUST:
/// <list type="bullet">
///   <item>Wrap the input stream in a <see cref="LimitedStream"/> before handing it to a parser
///   library, capped by <see cref="GranitTextExtractionOptions.MaxBodySizeBytes"/>.</item>
///   <item>Stop reading once the produced text exceeds <c>maxCharLength</c> and return a
///   <see cref="TextExtractionResult"/> with <see cref="TextExtractionResult.IsTruncated"/> set
///   to <c>true</c> — never throw on cap breach.</item>
///   <item>Resolve nothing from the network unless explicitly opted-in by the host (SSRF guard).</item>
/// </list>
/// </remarks>
public interface ITextExtractor
{
    /// <summary>
    /// Stable identifier surfaced on metrics tags, spans, and the resulting
    /// <see cref="TextExtractionResult.ExtractorName"/>. Convention: lowercase,
    /// dot-separated (e.g. <c>granit.text-extraction.plain-text</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns <c>true</c> when this extractor claims responsibility for the given
    /// MIME <paramref name="contentType"/>. The pipeline picks the first registered
    /// extractor whose <see cref="CanHandle"/> returns <c>true</c>.
    /// </summary>
    bool CanHandle(string contentType);

    /// <summary>
    /// Extracts plain text from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// Input byte stream. The implementation is responsible for wrapping this in a
    /// <see cref="LimitedStream"/>; callers MUST NOT pre-wrap (the pipeline does it).
    /// </param>
    /// <param name="contentType">The source MIME type.</param>
    /// <param name="maxCharLength">
    /// Maximum number of characters to produce. Extractors MUST stop reading once the produced
    /// text exceeds this and set <see cref="TextExtractionResult.IsTruncated"/> to <c>true</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default);
}
