using Granit.BlobStorage;
using Granit.Documents.Domain;
using Granit.Documents.Options;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// Cross-tenant maintenance operations consumed by the F9 background jobs. Bypasses the
/// multi-tenancy filter explicitly via <see cref="IgnoreQueryFiltersExtensions"/>.
/// </summary>
internal sealed partial class DocumentMaintenanceService(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    IBlobStorage blobStorage,
    ITenantQuotaService quotas,
    IClock clock,
    IOptions<GranitDocumentsOptions> options,
    ILogger<DocumentMaintenanceService> logger) : IDocumentMaintenanceService
{
    /// <inheritdoc />
    public Task<int> CleanupOrphanBlobsAsync(CancellationToken cancellationToken = default) =>
        blobStorage.CleanupOrphansAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> EmptyTrashAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.Now;
        DateTimeOffset cutoff = now.AddDays(-options.Value.TrashRetentionDays);

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Cross-tenant scan — the job runs without an ambient ICurrentTenant.
        List<Document> expired = await context.Documents
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(d => d.Status == DocumentStatus.Trashed && d.TrashedAt != null && d.TrashedAt < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return 0;
        }

        int deleted = 0;
        foreach (Document doc in expired)
        {
            List<DocumentVersion> versions = await context.DocumentVersions
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(v => v.DocumentId == doc.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            long releasedBytes = versions.Sum(v => v.SizeBytes);

            foreach (DocumentVersion version in versions)
            {
                await blobStorage.DeleteAsync(
                    DocumentService.ContainerName,
                    version.BlobDescriptorId,
                    deletionReason: $"Granit.Documents F9.2 empty-trash retention expiry for document {doc.Id}",
                    cancellationToken).ConfigureAwait(false);
            }

            doc.PermanentlyDelete(releasedBytes, now);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (releasedBytes > 0 && doc.TenantId is { } tid)
            {
                await quotas.DecrementAsync(tid, releasedBytes, cancellationToken).ConfigureAwait(false);
            }

            deleted++;
        }

        Log.TrashEmptied(logger, deleted);
        return deleted;
    }

    /// <inheritdoc />
    public async Task<int> RecomputeQuotasAsync(CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Aggregate live (non-permanently-deleted) version bytes per tenant. Trashed
        // versions still count — they only release on permanent delete.
        var actuals = await context.DocumentVersions
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Join(
                context.Documents.IgnoreQueryFilters([GranitFilterNames.MultiTenant]),
                v => v.DocumentId,
                d => d.Id,
                (v, d) => new { v.TenantId, v.SizeBytes, d.Status })
            .Where(x => x.Status != DocumentStatus.PermanentlyDeleted && x.TenantId != null)
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Sum(x => x.SizeBytes) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var actualsByTenant = actuals
            .Where(a => a.TenantId.HasValue)
            .ToDictionary(a => a.TenantId!.Value, a => a.Total);

        List<TenantStorageQuota> quotaRows = await context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int corrected = 0;
        foreach (TenantStorageQuota row in quotaRows)
        {
            if (row.TenantId is not { } tid)
            {
                continue;
            }
            long actual = actualsByTenant.TryGetValue(tid, out long total) ? total : 0;
            if (row.UsageBytes == actual)
            {
                continue;
            }

            long delta = actual - row.UsageBytes;
            if (delta > 0)
            {
                await quotas.IncrementAsync(tid, delta, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await quotas.DecrementAsync(tid, -delta, cancellationToken).ConfigureAwait(false);
            }
            corrected++;
        }

        if (corrected > 0)
        {
            Log.QuotasRecomputed(logger, corrected);
        }
        return corrected;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Granit.Documents F9.2 empty-trash deleted {Count} document(s).")]
        public static partial void TrashEmptied(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Granit.Documents F9.3 quota recompute corrected {Count} tenant row(s).")]
        public static partial void QuotasRecomputed(ILogger logger, int count);
    }
}
