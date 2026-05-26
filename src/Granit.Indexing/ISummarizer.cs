namespace Granit.Indexing;

/// <summary>
/// Produces an abstractive summary of an extracted document body. Optional indexing
/// enricher — backends ignore <see cref="IndexedEntry{TKey}.Summary"/> when it is
/// <c>null</c>.
/// </summary>
/// <remarks>
/// Concrete implementations ship in I-F3.2 (AI provider). The abstraction lives here so
/// consumers can register a custom summariser (e.g. domain-specific extractive model)
/// without depending on the AI package.
/// </remarks>
public interface ISummarizer
{
    /// <summary>
    /// Returns a short summary of <paramref name="content"/>, or <c>null</c> if the
    /// implementation declined (content too short, model unavailable, …).
    /// </summary>
    /// <param name="content">Body to summarise.</param>
    /// <param name="language">Optional ISO 639-1 hint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> SummarizeAsync(string content, string? language = null, CancellationToken cancellationToken = default);
}
