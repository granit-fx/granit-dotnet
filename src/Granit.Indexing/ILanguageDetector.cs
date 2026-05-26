namespace Granit.Indexing;

/// <summary>
/// Detects the natural language of a document body. Pluggable via priority chain — see
/// <see cref="CompositeLanguageDetector"/>.
/// </summary>
/// <remarks>
/// Backends consume the detected language to select an analyser/dictionary at index
/// time (Postgres tsvector dictionary, ES per-language analyser, …). When detection
/// returns <c>null</c>, the backend falls back to a language-agnostic mode (Postgres
/// <c>simple</c>) — searches still work, but stemming and stop-word handling degrade.
/// </remarks>
public interface ILanguageDetector
{
    /// <summary>
    /// Ordering used by <see cref="CompositeLanguageDetector"/>: higher priority wins.
    /// Concrete providers ship in I-F3.1+; the abstraction here lets consumers stack
    /// detectors (e.g. an explicit metadata-hint detector at <c>1000</c> overriding the
    /// Lingua-backed default at <c>100</c>).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns the detected ISO 639-1 code (<c>en</c>, <c>fr</c>, …) or <c>null</c> if
    /// detection was inconclusive. Implementations MUST be safe to call concurrently.
    /// </summary>
    /// <param name="content">Body to inspect. Implementations may sample the leading slice.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default);
}
