namespace Granit.Documents;

/// <summary>
/// Maintenance operations driven by recurring background jobs (F9). The implementation
/// (<c>Granit.Documents.EntityFrameworkCore.Internal.DocumentMaintenanceService</c>) reads
/// across tenants — these methods bypass the multi-tenancy filter intentionally.
/// </summary>
public interface IDocumentMaintenanceService
{
    /// <summary>
    /// Cleans up orphan blobs stuck in <c>Pending</c> / <c>Uploading</c> after the
    /// per-blob expiry — delegates to <c>IBlobStorage.CleanupOrphansAsync</c>. Documents-
    /// specific entry point so the F9.1 recurring schedule lives next to the rest of the
    /// module's maintenance jobs.
    /// </summary>
    /// <returns>The number of orphan blobs cleaned up.</returns>
    Task<int> CleanupOrphanBlobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes documents whose <c>TrashedAt</c> is older than the configured
    /// <c>TrashRetentionDays</c> window (F9.2). Iterates tenants, releases blob bytes via
    /// <c>IBlobStorage.DeleteAsync</c>, and decrements the per-tenant quota.
    /// </summary>
    /// <returns>The number of documents permanently deleted.</returns>
    Task<int> EmptyTrashAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles every tenant's <c>TenantStorageQuota.UsageBytes</c> with the actual
    /// sum of <c>DocumentVersion.SizeBytes</c> for active documents (F9.3). Drift can
    /// accumulate from rare failure paths (a transaction commits the version but the
    /// quota update fails); this is the authoritative correction.
    /// </summary>
    /// <returns>The number of quota rows whose <c>UsageBytes</c> changed during the recompute.</returns>
    Task<int> RecomputeQuotasAsync(CancellationToken cancellationToken = default);
}
