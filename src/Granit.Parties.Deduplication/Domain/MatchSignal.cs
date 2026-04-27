namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// One reason a candidate matched — surfaced in the admin UI so an operator can see WHY
/// the system flagged the pair as potential duplicates ("they share an email" vs "their
/// names are 0.91 Jaro-Winkler similar"). The aggregated score on
/// <see cref="DuplicateCandidate.Score"/> is the weighted sum across signals; the
/// per-signal contribution lives here.
/// </summary>
/// <param name="Kind">Stable identifier of the matching strategy. Conventional values:
/// <c>"EmailExact"</c>, <c>"PhoneExact"</c>, <c>"TaxIdExact"</c>,
/// <c>"NameTrigram"</c>, <c>"NameJaroWinkler"</c>, <c>"LastNameMetaphone"</c>,
/// <c>"AddressLevenshtein"</c>, <c>"EmailPartial"</c>, <c>"PhonePartial"</c>.</param>
/// <param name="Score">Per-signal contribution in <c>[0.0, 1.0]</c>.</param>
public sealed record MatchSignal(string Kind, decimal Score);
