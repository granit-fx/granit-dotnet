using Granit.LanguageDetection.Trigram.Internal;

namespace Granit.LanguageDetection.Trigram;

/// <summary>
/// Default <see cref="ILanguageDetector"/>: pure-managed trigram detector with
/// Unicode-script pre-filtering. Clean-room port of Franc (Titus Wormer 2014, MIT)
/// with the embedded language dataset attributed in <c>THIRD-PARTY-NOTICES.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coverage.</b> 390+ ISO 639-3 languages across 7 multi-language scripts and
/// 6 single-language scripts (see <c>README.md</c> for the exact list). The detector
/// returns an ISO 639-1 (alpha-2) code, ready for downstream alpha-2-keyed maps;
/// ISO 639-3 inputs without a 639-1 equivalent return <c>null</c> and the composite
/// chain falls through to the next provider.
/// </para>
/// <para>
/// <b>Priority.</b> Registered at <c>100</c> so explicit metadata-hint detectors
/// (priority &gt; 100) or AI-backed detectors win over this default.
/// </para>
/// <para>
/// <b>Determinism.</b> Same input always yields the same detected language. No
/// network round-trip, no native dependency, sub-millisecond latency on samples
/// up to <see cref="MaxSampleChars"/>.
/// </para>
/// </remarks>
public sealed class TrigramLanguageDetector : ILanguageDetectorProvider
{
    private const int DefaultMaxSampleChars = 2_048;
    private const int DefaultInputTopN = 300;

    private readonly LanguageProfileBundle _bundle;
    private readonly int _inputTopN;

    /// <summary>Builds a detector with the bundled Franc dataset loaded once.</summary>
    public TrigramLanguageDetector()
        : this(LanguageProfileLoader.Load(), DefaultMaxSampleChars, DefaultInputTopN)
    {
    }

    internal TrigramLanguageDetector(LanguageProfileBundle bundle, int maxSampleChars, int inputTopN)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSampleChars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(inputTopN);
        _bundle = bundle;
        MaxSampleChars = maxSampleChars;
        _inputTopN = inputTopN;
    }

    /// <inheritdoc/>
    public int Priority => 100;

    /// <summary>Maximum number of characters sampled from the head of the input.</summary>
    public int MaxSampleChars { get; }

    /// <inheritdoc/>
    public Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        if (content.Length < LanguageDetectorDefaults.MinimumSampleLength)
        {
            return Task.FromResult<string?>(null);
        }

        ReadOnlySpan<char> head = content.AsSpan(0, Math.Min(content.Length, MaxSampleChars));

        // 1) Find the dominant Unicode script. Single-language scripts return their
        //    ISO 639-3 directly without any trigram scoring.
        ScriptDetectionResult? script = ScriptDetector.Detect(head);
        if (script is null)
        {
            return Task.FromResult<string?>(null);
        }

        string? iso3 = script.SingleLanguageIso3 ?? ScoreLanguagesInScript(head, script.MultiLanguageScript!);
        if (iso3 is null)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(Iso639Map.ToIso639_1(iso3));
    }

    private string? ScoreLanguagesInScript(ReadOnlySpan<char> head, string scriptName)
    {
        if (!_bundle.ByScript.TryGetValue(scriptName, out IReadOnlyList<LanguageProfile>? profiles)
            || profiles.Count == 0)
        {
            return null;
        }

        Dictionary<string, int> inputRanks = TrigramExtractor.ExtractRanks(head, _inputTopN);
        return RankedDistanceScorer.Detect(inputRanks, profiles);
    }
}
