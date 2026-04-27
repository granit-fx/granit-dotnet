namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// Tier of the duplicate-detection pipeline that surfaced a candidate. Numerically ordered
/// so the orchestrator can prefer higher-confidence matches first.
/// </summary>
public enum DuplicateMatchTier
{
    /// <summary>
    /// Tier 1 — exact match on a canonicalised identifier (CanonicalEmail, CanonicalNumber,
    /// or normalised TaxId). Confidence is effectively 1.0; suitable for auto-suggesting a
    /// merge in the admin UI.
    /// </summary>
    Deterministic = 1,

    /// <summary>
    /// Tier 2 — pg_trgm similarity above the configured threshold on the Party Name. Acts
    /// as a blocking step that filters the cartesian product down to a tractable candidate
    /// set; the score reflects raw trigram similarity in <c>[0.0, 1.0]</c>. Always paired
    /// with a Tier-3 weighted-sum re-rank before surfacing to the admin.
    /// </summary>
    Blocking = 2,

    /// <summary>
    /// Tier 3 — weighted-sum scoring (Jaro-Winkler name + Double Metaphone last-name +
    /// normalised Levenshtein address + partial email/phone). Score in <c>[0.0, 1.0]</c>;
    /// values above <c>0.95</c> are auto-suggest candidates, <c>0.5..0.95</c> warrants
    /// manual review, below <c>0.5</c> is dropped.
    /// </summary>
    Fuzzy = 3,
}
