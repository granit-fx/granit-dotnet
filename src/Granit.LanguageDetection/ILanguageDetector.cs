namespace Granit.LanguageDetection;

/// <summary>
/// Detects the natural language of a text body. Pluggable via priority chain — see
/// <see cref="CompositeLanguageDetector"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-cutting concern.</b> This abstraction lives in its own package
/// (<c>Granit.LanguageDetection</c>) because language detection is useful beyond
/// indexing — notifications routing, localization classification, AI prompt
/// targeting, privacy data classification. Each consumer module declares
/// <c>[DependsOn(typeof(GranitLanguageDetectionModule))]</c> rather than pulling
/// in the larger indexing tree.
/// </para>
/// <para>
/// <b>Result format.</b> ISO 639-1 alpha-2 code (<c>en</c>, <c>fr</c>, <c>zh</c>, …)
/// or <c>null</c> when detection was inconclusive. Implementations MUST be safe to
/// call concurrently.
/// </para>
/// </remarks>
public interface ILanguageDetector
{
    /// <summary>
    /// Ordering used by <see cref="CompositeLanguageDetector"/>: higher priority wins.
    /// Consumers stack detectors (e.g. an explicit metadata-hint detector at
    /// <c>1000</c> overriding the default trigram detector at <c>100</c>).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Returns the detected ISO 639-1 code or <c>null</c> if detection was
    /// inconclusive. Implementations may sample the leading slice of large inputs.
    /// </summary>
    /// <remarks>
    /// <b>Caller responsibility — input size.</b> Implementations sample only the leading
    /// few KB of <paramref name="content"/> (the bundled trigram detector caps at 2 048
    /// chars), but the full string is still materialised in memory by the caller. Pre-
    /// truncate caller-controlled payloads (e.g. extracted document text) to a sensible
    /// bound — ~10 KB is more than enough for reliable detection — to avoid GC pressure
    /// on very large inputs (CWE-770).
    /// </remarks>
    Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default);
}
