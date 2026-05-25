using Granit.TextExtraction.Options;

namespace Granit.TextExtraction;

/// <summary>
/// Orchestrates the first-match-wins dispatch over the registered <see cref="ITextExtractor"/>
/// instances. Falls through to the plain-text fallback when no concrete extractor claims the
/// content type.
/// </summary>
/// <remarks>
/// Each extractor is responsible for wrapping its input in a <see cref="LimitedStream"/>
/// (capped by <see cref="GranitTextExtractionOptions.MaxBodySizeBytes"/>) before delegating to
/// any parser library.
/// </remarks>
public interface ITextExtractionPipeline
{
    /// <summary>
    /// Extracts plain text from <paramref name="source"/> using the first registered
    /// extractor that claims <paramref name="contentType"/>, or the plain-text fallback
    /// when none does.
    /// </summary>
    Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        CancellationToken cancellationToken = default);
}
