using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Parties.Deduplication.Internal;

/// <summary>
/// Composes the three matching tiers behind <see cref="IPartyDuplicateDetector"/>:
/// Tier-1 deterministic on canonical columns, Tier-2 pg_trgm blocking on Name, Tier-3
/// weighted-sum re-rank of the Tier-2 set. Tier-1 hits short-circuit straight to the
/// output (confidence already 1.0); Tier-2 hits feed Tier-3.
/// </summary>
internal sealed class DefaultPartyDuplicateDetector(
    IDbContextFactory<PartiesDbContext> contextFactory,
    Tier1DeterministicMatcher tier1,
    Tier2TrigramBlocker tier2,
    Tier3WeightedScorer tier3,
    IDataFilter dataFilter,
    ILogger<DefaultPartyDuplicateDetector> logger) : IPartyDuplicateDetector
{
    /// <summary>
    /// Hard cap on the number of parties scanned by <see cref="ScanTenantAsync"/> in v1.
    /// Tenants beyond this size should drive scans through the dedicated background job
    /// (story #1300) which paginates, checkpoints, and persists candidates incrementally.
    /// </summary>
    public const int TenantScanCap = 5_000;

    public async Task<IReadOnlyList<DuplicateCandidate>> FindCandidatesAsync(
        PartyDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        IReadOnlyList<DuplicateCandidate> tier1Hits =
            await tier1.MatchAsync(draft, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DuplicateCandidate> tier2Hits =
            await tier2.MatchAsync(draft, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<DuplicateCandidate> tier3Hits =
            await tier3.ScoreAsync(draft, tier2Hits, cancellationToken).ConfigureAwait(false);

        return MergeRanked(tier1Hits, tier3Hits);
    }

    public async Task<int> ScanTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Bypass the IMultiTenant filter for this single query — we need explicit control
        // because we're running per-tenant from a background context that may not have a
        // current tenant set (jobs typically don't). The IHasMergeTombstone filter stays
        // active so tombstoned losers don't show up as candidates.
        using IDisposable _ = dataFilter.Disable<IMultiTenant>();

        List<Party> tenantParties = await db.Parties
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(p => p.TenantId == tenantId)
            .Include(x => x.Emails)
            .Include(x => x.Phones)
            .Take(TenantScanCap + 1)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (tenantParties.Count > TenantScanCap)
        {
            logger.LogWarning(
                "Tenant {TenantId} has more than {Cap} parties; ScanTenantAsync truncated. "
                + "Drive large-tenant scans through the background job (#1300) instead.",
                tenantId, TenantScanCap);
            tenantParties = tenantParties.Take(TenantScanCap).ToList();
        }

        // Each pair is counted ONCE: the lower-id endpoint reports the match, the higher-
        // id endpoint is just the candidate. This mirrors how the merge endpoint orders
        // (survivor, loser) — picking the lower id as the "scanner" is arbitrary but
        // deterministic, which keeps the count reproducible across re-runs.
        HashSet<(Guid Lower, Guid Higher)> uniquePairs = new();

        foreach (Party party in tenantParties)
        {
            PartyDraft draft = ToDraft(party);
            IReadOnlyList<DuplicateCandidate> hits =
                await FindCandidatesAsync(draft, cancellationToken).ConfigureAwait(false);

            foreach (DuplicateCandidate hit in hits)
            {
                Guid candidate = hit.CandidateId.Value;
                if (candidate == party.Id)
                {
                    // Self-match — every party is its own deterministic dup. Skip.
                    continue;
                }

                (Guid lower, Guid higher) = candidate.CompareTo(party.Id) < 0
                    ? (candidate, party.Id)
                    : (party.Id, candidate);

                uniquePairs.Add((lower, higher));
            }
        }

        return uniquePairs.Count;
    }

    private static PartyDraft ToDraft(Party party) => new(
        TenantId: party.TenantId,
        Kind: party.Kind,
        Name: party.Name,
        TaxId: party.TaxId,
        Emails: [.. party.Emails.Select(e => e.Address)],
        Phones: [.. party.Phones.Select(p => p.Number)]);

    /// <summary>
    /// Merges Tier-1 and Tier-3 outputs into a single ranked list keyed by candidate id.
    /// On collision, Tier-1 wins (its score / signals are stronger evidence). Sorted by
    /// score descending so the UI can surface the most likely match first.
    /// </summary>
    private static IReadOnlyList<DuplicateCandidate> MergeRanked(
        IReadOnlyList<DuplicateCandidate> tier1,
        IReadOnlyList<DuplicateCandidate> tier3)
    {
        Dictionary<PartyId, DuplicateCandidate> byId = new();

        foreach (DuplicateCandidate t1 in tier1)
        {
            byId[t1.CandidateId] = t1;
        }

        foreach (DuplicateCandidate t3 in tier3)
        {
            // Tier-1 already has higher confidence — never overwrite.
            byId.TryAdd(t3.CandidateId, t3);
        }

        return [.. byId.Values.OrderByDescending(c => c.Score)];
    }
}
