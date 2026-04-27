using System.Text.Json;
using Granit.Guids;
using Granit.Parties.Deduplication.BackgroundJobs.Diagnostics;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.EntityFrameworkCore.Deduplication;
using Granit.Parties.EntityFrameworkCore.Entities;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.Deduplication.BackgroundJobs.Internal;

/// <summary>
/// EF-backed <see cref="IDuplicateCandidateSink"/>: persists candidate pairs into the
/// <c>parties_duplicate_candidates</c> table managed by <see cref="PartiesDbContext"/>.
/// Maintains the table's three core invariants:
/// <list type="bullet">
///   <item>pairs are stored ordered (lower-id first) so the same pair is never two rows;</item>
///   <item>candidates dismissed by an admin are NEVER reinserted — the dismissal sticks
///         across re-scans, so the admin's decision is durable;</item>
///   <item>re-detected pairs refresh score / signals / updated_at rather than insert
///         duplicates — the unique index on <c>(TenantId, PartyId, CandidateId, Tier)</c>
///         is the integrity backstop.</item>
/// </list>
/// </summary>
internal sealed class EfDuplicateCandidateSink(
    IDbContextFactory<PartiesDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IClock clock,
    PartiesDeduplicationMetrics metrics) : IDuplicateCandidateSink
{
    public async Task<int> UpsertAsync(
        IReadOnlyList<DuplicateCandidate> candidates,
        Guid sourcePartyId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return 0;
        }

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Normalise each candidate to the ordered pair (lower, higher) up front. Self-
        // matches (rare but possible if a tenant somehow contains the same GUID twice)
        // are filtered — the table constraint would reject them anyway.
        List<(Guid Lower, Guid Higher, DuplicateCandidate C)> ordered = new(candidates.Count);
        foreach (DuplicateCandidate c in candidates)
        {
            Guid candidateId = c.CandidateId.Value;
            if (candidateId == sourcePartyId)
            {
                continue;
            }

            (Guid lower, Guid higher) = candidateId.CompareTo(sourcePartyId) < 0
                ? (candidateId, sourcePartyId)
                : (sourcePartyId, candidateId);

            ordered.Add((lower, higher, c));
        }

        if (ordered.Count == 0)
        {
            return 0;
        }

        // Bulk read existing rows for these pairs in a single round-trip. Using the
        // (TenantId, PartyId, CandidateId) composite index, this stays O(log n) per pair.
        Guid[] lowers = [.. ordered.Select(x => x.Lower)];
        Guid[] highers = [.. ordered.Select(x => x.Higher)];

        List<PartyDuplicateCandidate> existing = await db.DuplicateCandidates
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(e => e.TenantId == tenantId
                && lowers.Contains(e.PartyId)
                && highers.Contains(e.CandidateId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<(Guid, Guid, int), PartyDuplicateCandidate> byKey = existing
            .ToDictionary(e => (e.PartyId, e.CandidateId, e.Tier));

        DateTimeOffset now = clock.Now;
        int upserted = 0;
        int dismissedSkipped = 0;

        foreach ((Guid lower, Guid higher, DuplicateCandidate c) in ordered)
        {
            int tierKey = (int)c.Tier;
            string signalsJson = JsonSerializer.Serialize(c.Signals);

            if (byKey.TryGetValue((lower, higher, tierKey), out PartyDuplicateCandidate? row))
            {
                if (row.DismissedAt is not null)
                {
                    // Admin dismissed this pair — don't re-suggest. Skip silently.
                    dismissedSkipped++;
                    continue;
                }

                row.Refresh(tierKey, c.Score, signalsJson, now);
                upserted++;
            }
            else
            {
                db.DuplicateCandidates.Add(PartyDuplicateCandidate.Create(
                    id: guidGenerator.Create(),
                    tenantId: tenantId,
                    partyId: lower,
                    candidateId: higher,
                    tier: tierKey,
                    score: c.Score,
                    signalsJson: signalsJson,
                    createdAt: now));
                upserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        metrics.RecordUpserted(tenantId, upserted);
        metrics.RecordDismissedSkipped(tenantId, dismissedSkipped);

        return upserted;
    }
}
