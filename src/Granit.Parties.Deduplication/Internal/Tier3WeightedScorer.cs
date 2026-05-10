using FuzzySharp;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Canonicalisation;
using Granit.Parties.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Levenshtein = SoftWx.Match.Levenshtein;

namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// Tier-3 of the duplicate-detection pipeline: weighted-sum re-rank of candidates produced
/// by Tier-1 and Tier-2. Loads each candidate's identifying fields (name, addresses,
/// canonical emails / phones) and computes a per-signal contribution; the aggregated score
/// determines whether the candidate is surfaced.
/// </summary>
/// <remarks>
/// Per-signal weights are documented in <see cref="DuplicateScoringWeights"/>. v1
/// substitutes Levenshtein-based ratios for Jaro-Winkler / Double Metaphone (no port of
/// either ships in FuzzySharp 2.x or SoftWx.Match 2.x); see the type-level remarks there.
/// </remarks>
internal sealed class Tier3WeightedScorer(IDbContextFactory<PartiesDbContext> contextFactory)
{
    private static readonly Levenshtein _levenshtein = new();

    /// <summary>
    /// Re-ranks <paramref name="candidates"/> against <paramref name="draft"/> and returns
    /// the survivors above <see cref="DuplicateScoringWeights.MinScore"/>, sorted by score
    /// descending.
    /// </summary>
    public async Task<IReadOnlyList<DuplicateCandidate>> ScoreAsync(
        PartyDraft draft,
        IReadOnlyList<DuplicateCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return [];
        }

        Guid[] candidateIds = [.. candidates.Select(c => c.CandidateId.Value)];

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Load every candidate with its emails, phones, and addresses in one round-trip.
        // The candidate set is bounded by Tier-2's MaxCandidates cap (50), so this is fine.
        List<Party> loaded = await db.Parties
            .Where(p => candidateIds.Contains(p.Id))
            .Include(p => p.Emails)
            .Include(p => p.Phones)
            .Include(p => p.Addresses)
            .AsSplitQuery()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var byId = loaded.ToDictionary(p => p.Id);

        List<DuplicateCandidate> ranked = new(candidates.Count);
        foreach (DuplicateCandidate candidate in candidates)
        {
            if (!byId.TryGetValue(candidate.CandidateId.Value, out Party? party))
            {
                // Candidate was tombstoned / archived between Tier-1/2 and Tier-3 — skip.
                continue;
            }

            (decimal score, IReadOnlyList<MatchSignal> signals) = Score(draft, party);

            if (score >= DuplicateScoringWeights.MinScore)
            {
                ranked.Add(new DuplicateCandidate(
                    CandidateId: candidate.CandidateId,
                    Score: score,
                    Tier: DuplicateMatchTier.Fuzzy,
                    Signals: signals));
            }
        }

        ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
        return ranked;
    }

    private static (decimal Score, IReadOnlyList<MatchSignal> Signals) Score(
        PartyDraft draft,
        Party party)
    {
        List<MatchSignal> signals = new(capacity: 5);
        decimal total = 0m;

        // ── Name (token-set ratio: order- and case-insensitive Levenshtein) ──
        decimal nameRatio = (decimal)Fuzz.TokenSetRatio(draft.Name, party.Name) / 100m;
        if (nameRatio > 0)
        {
            decimal contribution = nameRatio * DuplicateScoringWeights.Name;
            total += contribution;
            signals.Add(new MatchSignal("NameTokenSet", contribution));
        }

        // ── LastName (Levenshtein ratio on the last whitespace-separated token) ──
        decimal lastNameRatio = (decimal)Fuzz.Ratio(LastToken(draft.Name), LastToken(party.Name)) / 100m;
        if (lastNameRatio > 0)
        {
            decimal contribution = lastNameRatio * DuplicateScoringWeights.LastName;
            total += contribution;
            signals.Add(new MatchSignal("LastNameRatio", contribution));
        }

        // ── Address (normalised Levenshtein on AddressLine1 + PostalCode) ──
        if (!string.IsNullOrWhiteSpace(draft.AddressLine1))
        {
            string draftAddr = ($"{draft.AddressLine1} {draft.PostalCode}").Trim();
            decimal bestAddr = 0m;
            foreach (PartyAddress addr in party.Addresses)
            {
                string partyAddr = ($"{addr.Value.Line1} {addr.Value.PostalCode}").Trim();
                if (partyAddr.Length == 0)
                {
                    continue;
                }
                decimal sim = (decimal)_levenshtein.Similarity(
                    draftAddr.ToLowerInvariant(), partyAddr.ToLowerInvariant());
                if (sim > bestAddr) { bestAddr = sim; }
            }
            if (bestAddr > 0)
            {
                decimal contribution = bestAddr * DuplicateScoringWeights.Address;
                total += contribution;
                signals.Add(new MatchSignal("AddressLevenshtein", contribution));
            }
        }

        // ── Email partial (best partial-substring match across canonical emails) ──
        if (draft.Emails is { Count: > 0 } && party.Emails.Count > 0)
        {
            decimal best = BestEmailPartial(draft.Emails, party.Emails);
            if (best > 0)
            {
                decimal contribution = best * DuplicateScoringWeights.EmailPartial;
                total += contribution;
                signals.Add(new MatchSignal("EmailPartial", contribution));
            }
        }

        // ── Phone partial (last-7-digits exact match — skips country-code variance) ──
        if (draft.Phones is { Count: > 0 } && party.Phones.Count > 0
            && AnyPhoneSubscriberMatch(draft.Phones, party.Phones))
        {
            decimal contribution = DuplicateScoringWeights.PhonePartial;
            total += contribution;
            signals.Add(new MatchSignal("PhonePartial", contribution));
        }

        return (total, signals);
    }

    private static string LastToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        int idx = value.LastIndexOf(' ');
        return idx < 0 ? value : value[(idx + 1)..];
    }

    private static decimal BestEmailPartial(
        IReadOnlyList<string> draftEmails,
        IReadOnlyList<PartyEmail> partyEmails)
    {
        decimal best = 0m;
        foreach (string draftRaw in draftEmails)
        {
            string? draftCanonical = EmailCanonicaliser.Canonicalise(draftRaw);
            if (draftCanonical is null) { continue; }

            foreach (PartyEmail e in partyEmails)
            {
                string? partyCanonical = e.CanonicalEmail
                    ?? EmailCanonicaliser.Canonicalise(e.Address);
                if (partyCanonical is null) { continue; }

                decimal ratio = (decimal)Fuzz.PartialRatio(draftCanonical, partyCanonical) / 100m;
                if (ratio > best) { best = ratio; }
            }
        }
        return best;
    }

    private static bool AnyPhoneSubscriberMatch(
        IReadOnlyList<string> draftPhones,
        IReadOnlyList<PartyPhone> partyPhones)
    {
        foreach (string draftRaw in draftPhones)
        {
            string? draftCanonical = PhoneCanonicaliser.Canonicalise(draftRaw);
            string draftSuffix = LastDigits(draftCanonical, count: 7);
            if (draftSuffix.Length == 0) { continue; }

            foreach (PartyPhone p in partyPhones)
            {
                string partySuffix = LastDigits(
                    p.CanonicalNumber ?? PhoneCanonicaliser.Canonicalise(p.Number),
                    count: 7);
                if (partySuffix.Length == 0) { continue; }

                if (string.Equals(draftSuffix, partySuffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string LastDigits(string? phone, int count)
    {
        if (string.IsNullOrEmpty(phone))
        {
            return string.Empty;
        }
        int seen = 0;
        Span<char> buf = stackalloc char[count];
        for (int i = phone.Length - 1; i >= 0 && seen < count; i--)
        {
            if (char.IsDigit(phone[i]))
            {
                buf[count - 1 - seen] = phone[i];
                seen++;
            }
        }
        return seen < count ? string.Empty : new string(buf);
    }
}
