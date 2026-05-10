using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Canonicalisation;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// Tier-1 of the duplicate-detection pipeline: exact lookup on the canonical projection
/// columns populated by <c>PartyCanonicalisationInterceptor</c> (story #1297). Returns
/// zero or more <see cref="DuplicateCandidate"/>s with <see cref="DuplicateMatchTier.Deterministic"/>
/// and a confidence of <c>1.0</c> per match.
/// </summary>
/// <remarks>
/// <para>
/// Because canonical columns are indexed (<c>IX_*_canonicalemail</c>,
/// <c>IX_*_canonicalnumber</c>, <c>IX_*_taxid</c>), each lookup is O(log n) and the whole
/// tier completes in milliseconds even on large tenants. The output may contain duplicates
/// (the same party matching multiple input fields) — the composer in
/// <c>DefaultPartyDuplicateDetector</c> deduplicates by <c>CandidateId</c> before returning.
/// </para>
/// <para>
/// The matcher operates against the same <see cref="Granit.MultiTenancy.IMultiTenant"/>
/// query filter that protects every other read in the application — adding an explicit
/// <c>TenantId</c> filter would be redundant. The DbContext is resolved via
/// <see cref="IDbContextFactory{TContext}"/> (scoped) so the detector can run inside
/// background-job or HTTP-request scopes interchangeably.
/// </para>
/// </remarks>
internal sealed class Tier1DeterministicMatcher(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IPartyLookupHasher hasher)
{
    public async Task<IReadOnlyList<DuplicateCandidate>> MatchAsync(
        PartyDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // No canonical inputs → no Tier-1 hits possible. Skip the DB round-trip.
        // Email / phone canonical columns are encrypted at rest; equality lookups
        // route through the parallel CanonicalEmailHash / CanonicalNumberHash
        // index columns (peppered HMAC).
        string? canonicalTaxId = TaxIdCanonicaliser.Canonicalise(draft.TaxId);
        List<string> emailHashes = HashAll(draft.Emails, EmailCanonicaliser.Canonicalise);
        List<string> phoneHashes = HashAll(draft.Phones, PhoneCanonicaliser.Canonicalise);

        if (canonicalTaxId is null && emailHashes.Count == 0 && phoneHashes.Count == 0)
        {
            return [];
        }

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, List<MatchSignal>> hits = new();
        Guid? draftTenantId = draft.TenantId;

        // Every lookup is explicitly scoped to draft.TenantId — the contract on
        // IPartyDuplicateDetector guarantees cross-tenant matches are NEVER surfaced. We
        // depend on explicit equality rather than the ambient IMultiTenant filter, because
        // the scan path runs with the filter disabled and a different tenant in scope.

        // ── TaxId — direct on Party (canonical form lives in TaxId itself, single column).
        if (canonicalTaxId is not null)
        {
            List<Guid> taxIdMatches = await db.Parties
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(p => p.TenantId == draftTenantId && p.TaxId == canonicalTaxId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (Guid id in taxIdMatches)
            {
                AddSignal(hits, id, "TaxIdExact");
            }
        }

        // ── Email — joined via the parent Party so we can apply the tenant filter on the
        // parent (the child has no tenant column). Query keyed on CanonicalEmailHash
        // because the encrypted CanonicalEmail column cannot serve IN (…) lookups.
        if (emailHashes.Count > 0)
        {
            List<Guid> emailMatches = await db.Parties
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(p => p.TenantId == draftTenantId
                    && p.Emails.Any(e => e.CanonicalEmailHash != null && emailHashes.Contains(e.CanonicalEmailHash)))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (Guid id in emailMatches)
            {
                AddSignal(hits, id, "EmailExact");
            }
        }

        // ── Phone — same shape as email, keyed on CanonicalNumberHash.
        if (phoneHashes.Count > 0)
        {
            List<Guid> phoneMatches = await db.Parties
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(p => p.TenantId == draftTenantId
                    && p.Phones.Any(ph => ph.CanonicalNumberHash != null && phoneHashes.Contains(ph.CanonicalNumberHash)))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (Guid id in phoneMatches)
            {
                AddSignal(hits, id, "PhoneExact");
            }
        }

        return [..
            hits.Select(kv => new DuplicateCandidate(
                CandidateId: PartyId.Create(kv.Key),
                Score: 1.0m,
                Tier: DuplicateMatchTier.Deterministic,
                Signals: kv.Value))];
    }

    private List<string> HashAll(
        IReadOnlyList<string>? inputs,
        Func<string?, string?> canonicaliser)
    {
        if (inputs is null || inputs.Count == 0)
        {
            return [];
        }

        List<string> result = new(inputs.Count);
        foreach (string raw in inputs)
        {
            string? hash = hasher.ComputeHash(canonicaliser(raw));
            if (hash is not null)
            {
                result.Add(hash);
            }
        }

        return result;
    }

    private static void AddSignal(
        Dictionary<Guid, List<MatchSignal>> hits,
        Guid partyId,
        string kind)
    {
        if (!hits.TryGetValue(partyId, out List<MatchSignal>? signals))
        {
            signals = new List<MatchSignal>(capacity: 4);
            hits[partyId] = signals;
        }
        signals.Add(new MatchSignal(kind, 1.0m));
    }
}
