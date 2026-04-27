namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// Per-signal weights for the Tier-3 weighted-sum scorer. They sum to <c>1.0</c> by design
/// so the aggregated score stays in <c>[0.0, 1.0]</c> — the same scale Tier-1 (1.0) and
/// Tier-2 (raw trigram similarity) report on.
/// </summary>
/// <remarks>
/// <para>
/// Story #1299 originally specified Jaro-Winkler on Name (0.4) + Double Metaphone on
/// LastName (0.2) + normalised Levenshtein on Address (0.2) + Email partial (0.1) +
/// Phone partial (0.1). Neither FuzzySharp 2.x nor SoftWx.Match 2.x ships Jaro-Winkler
/// or Double Metaphone, so this v1 substitutes:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Name (0.4)</b> — <c>FuzzySharp.Fuzz.TokenSetRatio</c>: order- and
///     case-insensitive token-aware Levenshtein. Catches "Jean Dupont" vs "DUPONT, Jean"
///     vs "Jean DUPONT Sr" — close enough to JW for the v1 use case (exact-match domain
///     hits stay in Tier-1; Tier-3's job is to rank fuzzy candidates).
///   </item>
///   <item>
///     <b>LastName (0.2)</b> — <c>FuzzySharp.Fuzz.Ratio</c> on the last whitespace-
///     separated token of <c>Name</c>. Without Double Metaphone we lose phonetic robustness
///     (e.g. "Smith" vs "Smyth"), but Levenshtein still catches small typos.
///   </item>
///   <item>
///     <b>Address (0.2)</b> — <c>SoftWx.Match.Levenshtein.Similarity</c> on
///     <c>"{AddressLine1} {PostalCode}"</c>. Strict normalised similarity.
///   </item>
///   <item>
///     <b>Email partial (0.1)</b> — <c>FuzzySharp.Fuzz.PartialRatio</c> on the
///     canonical-email projection. Best-substring match catches alias / dot variants.
///   </item>
///   <item>
///     <b>Phone partial (0.1)</b> — last-7-digits exact comparison on the canonical
///     E.164 form. Skips the country-code variability so "+33 6 …" and "+1 …" never
///     collide just because the local part overlaps.
///   </item>
/// </list>
/// <para>
/// Adding Jaro-Winkler + Double Metaphone is tracked as a future enhancement: a tiny
/// inline JW implementation (~50 lines) is the standard fix and would shift weights to
/// the original 0.4/0.2 split between full-name JW and last-name Metaphone. Until then,
/// the v1 numbers below are the authoritative source — drift is enforced by the
/// <c>SumsToOne</c> test.
/// </para>
/// </remarks>
internal static class DuplicateScoringWeights
{
    /// <summary>Token-set ratio on the full Name. Drives most of the score in v1.</summary>
    public const decimal Name = 0.4m;

    /// <summary>Levenshtein ratio on the last token of Name (lieu of Double Metaphone).</summary>
    public const decimal LastName = 0.2m;

    /// <summary>Normalised Levenshtein on AddressLine1 + PostalCode.</summary>
    public const decimal Address = 0.2m;

    /// <summary>Best-substring partial Levenshtein on canonical emails.</summary>
    public const decimal EmailPartial = 0.1m;

    /// <summary>Last-7-digits exact comparison on canonical E.164 phone numbers.</summary>
    public const decimal PhonePartial = 0.1m;

    /// <summary>Threshold below which Tier-3 candidates are dropped entirely.</summary>
    public const decimal MinScore = 0.5m;
}
