using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Retention;

/// <summary>
/// Export-side reads/writes for the GDPR retention sweep. Runs with no ambient tenant, so every
/// query goes through <see cref="EfStoreBase{TEntity,TContext}.QueryAcrossTenants"/> (see the
/// cross-tenant contract documented on <c>IDataExchangeRetentionStore</c>).
/// </summary>
internal sealed class EfExportJobRetentionStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<ExportJob, DataExchangeDbContext>(contextFactory, currentTenant)
{
    private static readonly ExportJobStatus[] TerminalStatuses =
    [
        ExportJobStatus.Completed,
        ExportJobStatus.Failed,
    ];

    /// <summary>
    /// Completed export jobs whose generated file has not been purged yet, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<ExportJob>> GetJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            db => QueryAcrossTenants(db)
                .AsNoTracking()
                .Where(j => j.Status == ExportJobStatus.Completed
                    && j.BlobReference != null
                    && j.FileDeletedAt == null
                    && j.CompletedAt < completedBefore)
                .OrderBy(j => j.CompletedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Terminal export jobs past the record-retention window, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<ExportJob>> GetExpiredTerminalJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            db => QueryAcrossTenants(db)
                .AsNoTracking()
                .Where(j => TerminalStatuses.Contains(j.Status) && j.CompletedAt < terminalBefore)
                .OrderBy(j => j.CompletedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Export jobs stranded in <see cref="ExportJobStatus.Exporting"/>, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<ExportJob>> GetStuckJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            db => QueryAcrossTenants(db)
                .AsNoTracking()
                .Where(j => j.Status == ExportJobStatus.Exporting && (j.ModifiedAt ?? j.CreatedAt) < stuckBefore)
                .OrderBy(j => j.ModifiedAt ?? j.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    public new Task UpdateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        base.UpdateAsync(job, cancellationToken);

    public new Task DeleteAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        base.DeleteAsync(job, cancellationToken);
}
