using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties.Deduplication.Domain;

/// <summary>
/// A potential duplicate of the party being investigated, surfaced by one of the three
/// detection tiers (<see cref="DuplicateMatchTier"/>). Returned by
/// <see cref="IPartyDuplicateDetector.FindCandidatesAsync"/> and persisted by the upcoming
/// background scan job (<see href="https://github.com/granit-fx/granit-dotnet/issues/1300">#1300</see>).
/// </summary>
/// <param name="CandidateId">Existing party that <em>may</em> be a duplicate of the input.</param>
/// <param name="Score">Aggregated confidence in <c>[0.0, 1.0]</c>. Tier-1 hits are <c>1.0</c>;
/// Tier-2 carries the trigram similarity; Tier-3 carries the weighted-sum.</param>
/// <param name="Tier">Which tier surfaced this candidate — also encodes confidence ordering.</param>
/// <param name="Signals">Per-signal contributions. Empty list valid for Tier-1 self-evident matches.</param>
public sealed record DuplicateCandidate(
    PartyId CandidateId,
    decimal Score,
    DuplicateMatchTier Tier,
    IReadOnlyList<MatchSignal> Signals);
