using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;

namespace Granit.DataExchange.Retention.Internal;

/// <summary>
/// Default implementation of <see cref="IDataExchangeRetentionStore"/>.
/// Throws <see cref="NotImplementedException"/> — retention sweeps require durable persistence,
/// so a silent no-op here would let a host believe GDPR retention is enforced when nothing runs.
/// </summary>
internal sealed class NullDataExchangeRetentionStore : IDataExchangeRetentionStore
{
    private const string Message =
        "Data exchange retention requires a durable IDataExchangeRetentionStore. " +
        "Call builder.AddGranitDataExchangeEntityFrameworkCore(...) (Granit.DataExchange.EntityFrameworkCore) " +
        "to register the EF Core implementation.";

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportJob>> GetImportJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExportJob>> GetExportJobsWithExpiredFilesAsync(
        DateTimeOffset completedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportJob>> GetExpiredTerminalImportJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExportJob>> GetExpiredTerminalExportJobsAsync(
        DateTimeOffset terminalBefore, int batchSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ImportJob>> GetStuckImportJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExportJob>> GetStuckExportJobsAsync(
        DateTimeOffset stuckBefore, int batchSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task UpdateImportJobAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task UpdateExportJobAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task DeleteImportJobAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task DeleteExportJobAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);
}
