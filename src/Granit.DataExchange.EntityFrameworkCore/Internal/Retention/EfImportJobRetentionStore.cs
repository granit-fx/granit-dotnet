using Granit.DataExchange.Import.Domain;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Retention;

/// <summary>
/// Import-side reads/writes for the GDPR retention sweep. Runs with no ambient tenant, so every
/// query goes through <see cref="EfStoreBase{TEntity,TContext}.QueryAcrossTenants"/> (see the
/// cross-tenant contract documented on <c>IDataExchangeRetentionStore</c>).
/// </summary>
internal sealed class EfImportJobRetentionStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<ImportJob, DataExchangeDbContext>(contextFactory, currentTenant)
{
    private static readonly ImportJobStatus[] TerminalStatuses =
    [
        ImportJobStatus.Completed,
        ImportJobStatus.PartiallyCompleted,
        ImportJobStatus.Failed,
        ImportJobStatus.Cancelled,
    ];

    /// <summary>
    /// Terminal import jobs whose file has not been purged yet, oldest first.
    /// </summary>
    /// <remarks>
    /// <c>Cancel()</c> never sets <see cref="ImportJob.CompletedAt"/>, so a
    /// cancelled job's file would otherwise never expire — falls back to
    /// <see cref="CreationAuditedEntity.CreatedAt"/> for those rows, mirroring
    /// <see cref="GetExpiredTerminalJobsAsync"/>.
    /// </remarks>
    public async Task<IReadOnlyList<ImportJob>> GetJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            db => QueryAcrossTenants(db)
                .AsNoTracking()
                .Where(j => TerminalStatuses.Contains(j.Status)
                    && j.FileDeletedAt == null
                    && (j.CompletedAt ?? j.CreatedAt) < completedBefore)
                .OrderBy(j => j.CompletedAt ?? j.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Terminal import jobs past the record-retention window, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<ImportJob>> GetExpiredTerminalJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            db => QueryAcrossTenants(db)
                .AsNoTracking()
                .Where(j => TerminalStatuses.Contains(j.Status) && (j.CompletedAt ?? j.CreatedAt) < terminalBefore)
                .OrderBy(j => j.CompletedAt ?? j.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Import jobs stranded in <see cref="ImportJobStatus.Executing"/>, oldest first.
    /// </summary>
    public async Task<IReadOnlyList<ImportJob>> GetStuckJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default) =>
        await ReadAsync(
            db => QueryAcrossTenants(db)
                .AsNoTracking()
                .Where(j => j.Status == ImportJobStatus.Executing && (j.ModifiedAt ?? j.CreatedAt) < stuckBefore)
                .OrderBy(j => j.ModifiedAt ?? j.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    public new Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        base.UpdateAsync(job, cancellationToken);

    public new Task DeleteAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        base.DeleteAsync(job, cancellationToken);
}
