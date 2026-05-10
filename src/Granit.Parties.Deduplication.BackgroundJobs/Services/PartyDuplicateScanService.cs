using System.Diagnostics;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Deduplication.BackgroundJobs.Diagnostics;
using Granit.Parties.Deduplication.Domain;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Parties.Deduplication.BackgroundJobs.Services;

/// <summary>
/// Public contract for the per-tenant duplicate scan service — separated from the impl so
/// the handler can take a dependency on a public type without the impl needing to expose
/// its internal-only constructor parameters (notably <c>IDbContextFactory&lt;PartiesDbContext&gt;</c>).
/// </summary>
public interface IPartyDuplicateScanService
{
    /// <summary>Scans every active tenant and returns the total upserted candidate count.</summary>
    Task<int> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>Scans a single tenant. <paramref name="tenantId"/> is <c>null</c> for the host scope.</summary>
    Task<int> ScanTenantAsync(Guid? tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Recurring scan that materialises the <c>parties_duplicate_candidates</c> table for every
/// active tenant. Runs on the schedule defined by
/// <see cref="Jobs.PartyDuplicateScanJob"/> (default: 03:00 daily). Iterates each tenant,
/// loads its alive parties, runs the detector pipeline against each, and upserts the
/// resulting candidates through <see cref="IDuplicateCandidateSink"/>. Dismissed pairs are
/// preserved across runs so admin decisions stick.
/// </summary>
internal sealed class PartyDuplicateScanService(
    ITenantEnumerator tenantEnumerator,
    IDbContextFactory<PartiesDbContext> contextFactory,
    IPartyDuplicateDetector detector,
    IDuplicateCandidateSink sink,
    IDataFilter dataFilter,
    PartiesDeduplicationMetrics metrics,
    ILogger<PartyDuplicateScanService> logger) : IPartyDuplicateScanService
{
    /// <summary>Hard cap on the parties scanned per tenant per run. Tenants beyond this
    /// size emit a warning and are truncated — large-tenant scans are intentionally a
    /// future follow-up (paginated incremental scan with checkpoint).</summary>
    public const int TenantPartyCap = 5_000;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        int totalUpserted = 0;
        int tenantCount = 0;

        // Always include the host tenant (TenantId == null).
        totalUpserted += await ScanTenantAsync(null, cancellationToken).ConfigureAwait(false);
        tenantCount++;

        await foreach (Guid tenantId in tenantEnumerator.GetActiveTenantIdsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalUpserted += await ScanTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
            tenantCount++;
        }

        logger.LogInformation(
            "Party duplicate scan completed: {TenantCount} tenant(s), {Upserted} candidate pair(s) upserted.",
            tenantCount, totalUpserted);

        return totalUpserted;
    }

    /// <summary>Runs the detector pipeline against every alive party of <paramref name="tenantId"/>.</summary>
    public async Task<int> ScanTenantAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        await using PartiesDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Ambient IMultiTenant filter is bypassed: the job runs without a current tenant
        // and the scan is parameterised by tenantId. Explicit Where(p => p.TenantId == ...)
        // is the only tenant filter applied. Tombstoned parties (IHasMergeTombstone)
        // remain filtered out — we don't surface duplicates of merged-out aggregates.
        using IDisposable _ = dataFilter.Disable<IMultiTenant>();

        List<Party> parties = await db.Parties
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Emails)
            .Include(p => p.Phones)
            .Take(TenantPartyCap + 1)
            .AsSplitQuery()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (parties.Count > TenantPartyCap)
        {
            logger.LogWarning(
                "Tenant {TenantId} has more than {Cap} parties; scan truncated. "
                + "Paginated incremental scan is a follow-up enhancement.",
                tenantId, TenantPartyCap);
            parties = parties.Take(TenantPartyCap).ToList();
        }

        int upserted = 0;
        foreach (Party party in parties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PartyDraft draft = ToDraft(party);
            IReadOnlyList<DuplicateCandidate> candidates =
                await detector.FindCandidatesAsync(draft, cancellationToken).ConfigureAwait(false);

            // Filter out the self-match before we hand the list to the sink. The sink also
            // guards against this, but eliminating it here keeps the metrics accurate.
            var nonSelf = candidates
                .Where(c => c.CandidateId.Value != party.Id)
                .ToList();

            if (nonSelf.Count == 0)
            {
                continue;
            }

            upserted += await sink.UpsertAsync(nonSelf, party.Id, tenantId, cancellationToken)
                .ConfigureAwait(false);
        }

        stopwatch.Stop();
        metrics.RecordScanDuration(tenantId, stopwatch.Elapsed);
        return upserted;
    }

    private static PartyDraft ToDraft(Party party) => new(
        TenantId: party.TenantId,
        Kind: party.Kind,
        Name: party.Name,
        TaxId: party.TaxId,
        Emails: [.. party.Emails.Select(e => e.Address)],
        Phones: [.. party.Phones.Select(p => p.Number)]);
}
