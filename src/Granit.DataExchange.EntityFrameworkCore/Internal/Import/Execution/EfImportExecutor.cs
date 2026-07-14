using System.Diagnostics;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Execution;

/// <summary>
/// EF Core implementation of <see cref="IImportExecutor{TEntity}"/>.
/// Persists successful row outcomes in batches using the application DbContext;
/// failed and skipped outcomes are counted into the report without a persistence attempt.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type.</typeparam>
internal sealed class EfImportExecutor<TEntity, TContext>(
    IDbContextFactory<TContext> contextFactory) : IImportExecutor<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    /// <inheritdoc/>
    public async Task<ImportReport> ExecuteAsync(
        IAsyncEnumerable<RowOutcome<TEntity>> rows,
        ImportExecutionOptions options,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        List<ImportRowError> errors = [];
        int totalRows = 0;
        int succeededRows = 0;
        int failedRows = 0;
        int skippedRows = 0;
        int insertedRows = 0;
        int updatedRows = 0;
        int batchCount = 0;

        await using TContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await foreach (RowOutcome<TEntity> row in rows.WithCancellation(cancellationToken))
            {
                totalRows++;

                if (row.IsSkipped)
                {
                    skippedRows++;
                    continue;
                }

                if (row.Error is not null || row.Entity is null)
                {
                    failedRows++;
                    errors.Add(row.Error ?? new ImportRowError(
                        row.RowNumber, ImportRowErrorKind.Conversion,
                        ["Granit:DataExchange:Conversion:Unexpected"],
                        "Row outcome carried neither an entity nor an error."));
                    continue;
                }

                try
                {
                    if (IsUpdateOperation(row))
                    {
                        context.Entry(row.Identity!.ExistingEntity!).CurrentValues.SetValues(row.Entity);
                        updatedRows++;
                    }
                    else
                    {
                        context.Set<TEntity>().Add(row.Entity);
                        insertedRows++;
                    }

                    succeededRows++;
                    batchCount++;

                    if (batchCount >= options.BatchSize)
                    {
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        batchCount = 0;

                        progress?.Report(new ImportProgress(
                            totalRows, 0, succeededRows, failedRows));
                    }
                }
                catch (DbUpdateException ex)
                {
                    failedRows++;
                    succeededRows--;
                    errors.Add(new ImportRowError(
                        row.RowNumber,
                        ImportRowErrorKind.Persistence,
                        ["Granit:DataExchange:PersistenceError"],
                        ex.InnerException?.Message ?? ex.Message));

                    if (options.ErrorBehavior == ImportErrorBehavior.FailFast)
                    {
                        break;
                    }

                    // Detach the failed entity to continue processing
                    DetachFailedEntities(context);
                }
            }

            // Save remaining batch
            if (batchCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await CommitOrRollbackAsync(transaction, options.DryRun, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Roll back and rethrow: the orchestrator folds unexpected exceptions into a
            // failed report and logs them — swallowing here produced false-success reports.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        stopwatch.Stop();

        progress?.Report(new ImportProgress(totalRows, totalRows, succeededRows, failedRows));

        return new ImportReport
        {
            TotalRows = totalRows,
            SucceededRows = succeededRows,
            FailedRows = failedRows,
            SkippedRows = skippedRows,
            InsertedRows = insertedRows,
            UpdatedRows = updatedRows,
            Duration = stopwatch.Elapsed,
            FinalStatus = DetermineFinalStatus(failedRows, succeededRows),
            RowErrors = errors.AsReadOnly(),
        };
    }

    private static bool IsUpdateOperation(RowOutcome<TEntity> row) =>
        row.Identity?.Operation == RecordOperation.Update && row.Identity.ExistingEntity is not null;

    private static async Task CommitOrRollbackAsync(
        IDbContextTransaction transaction, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static ImportJobStatus DetermineFinalStatus(int failedRows, int succeededRows)
    {
        if (failedRows == 0)
        {
            return ImportJobStatus.Completed;
        }

        return succeededRows > 0
            ? ImportJobStatus.PartiallyCompleted
            : ImportJobStatus.Failed;
    }

    private static void DetachFailedEntities(TContext context)
    {
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry in
            context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.State = EntityState.Detached;
        }
    }
}
