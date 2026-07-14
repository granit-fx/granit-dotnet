using Granit.DataExchange.Import.Reporting;

namespace Granit.DataExchange.Import.Execution;

/// <summary>
/// Persists row outcomes to the database in batches and folds every outcome —
/// successful, failed, or skipped — into the final <see cref="ImportReport"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IImportExecutor<TEntity> where TEntity : class
{
    /// <summary>
    /// Executes the import by persisting successful rows and accounting for failed/skipped ones.
    /// </summary>
    /// <param name="rows">The row outcomes to process (consumed as a stream).</param>
    /// <param name="options">Execution options (batch size, dry-run, error behavior).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The import report with statistics and row-level errors.</returns>
    Task<ImportReport> ExecuteAsync(
        IAsyncEnumerable<RowOutcome<TEntity>> rows,
        ImportExecutionOptions options,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
