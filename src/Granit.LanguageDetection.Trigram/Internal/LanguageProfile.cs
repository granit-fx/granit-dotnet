using System.Collections.Frozen;

namespace Granit.LanguageDetection.Trigram.Internal;

/// <summary>
/// Pre-loaded language trigram model: rank table for the most-frequent trigrams.
/// Built once per language at startup, immutable afterwards.
/// </summary>
/// <param name="Iso639_3Code">ISO 639-3 code (matches the upstream Franc dataset).</param>
/// <param name="TrigramRanks">Trigram → 0-based rank table; lower = more frequent.</param>
internal sealed record LanguageProfile(string Iso639_3Code, FrozenDictionary<string, int> TrigramRanks);

/// <summary>
/// All language profiles grouped by script — the runtime layout used by the
/// distance scorer.
/// </summary>
internal sealed record LanguageProfileBundle(IReadOnlyDictionary<string, IReadOnlyList<LanguageProfile>> ByScript);
