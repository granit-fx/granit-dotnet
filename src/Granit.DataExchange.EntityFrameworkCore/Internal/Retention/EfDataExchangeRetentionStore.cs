using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Retention;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Retention;

/// <summary>
/// EF Core implementation of <see cref="IDataExchangeRetentionStore"/>. A thin facade delegating
/// to the import-side and export-side stores — kept separate so each can be constructed against
/// its own <c>EfStoreBase&lt;TEntity, TContext&gt;</c> generic specialization.
/// </summary>
internal sealed class EfDataExchangeRetentionStore(
    EfImportJobRetentionStore importSide,
    EfExportJobRetentionStore exportSide) : IDataExchangeRetentionStore
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportJob>> GetImportJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        importSide.GetJobsWithExpiredFilesAsync(completedBefore, batchSize, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExportJob>> GetExportJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        exportSide.GetJobsWithExpiredFilesAsync(completedBefore, batchSize, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportJob>> GetExpiredTerminalImportJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default) =>
        importSide.GetExpiredTerminalJobsAsync(terminalBefore, batchSize, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExportJob>> GetExpiredTerminalExportJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default) =>
        exportSide.GetExpiredTerminalJobsAsync(terminalBefore, batchSize, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportJob>> GetStuckImportJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default) =>
        importSide.GetStuckJobsAsync(stuckBefore, batchSize, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExportJob>> GetStuckExportJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default) =>
        exportSide.GetStuckJobsAsync(stuckBefore, batchSize, cancellationToken);

    /// <inheritdoc/>
    public Task UpdateImportJobAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        importSide.UpdateAsync(job, cancellationToken);

    /// <inheritdoc/>
    public Task UpdateExportJobAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        exportSide.UpdateAsync(job, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteImportJobAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        importSide.DeleteAsync(job, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteExportJobAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        exportSide.DeleteAsync(job, cancellationToken);
}
