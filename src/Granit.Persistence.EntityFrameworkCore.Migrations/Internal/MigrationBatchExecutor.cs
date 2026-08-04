using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Default <see cref="IMigrationBatchExecutor"/> implementation.
/// </summary>
internal sealed partial class MigrationBatchExecutor(
    IMigrationCycleRegistry registry,
    IServiceProvider serviceProvider,
    MigrationProgressDbContext progressContext,
    ITenantDbIsolator isolator,
    IClock clock,
    IGuidGenerator guidGenerator,
    IOptions<MigrationStartupOptions> options,
    ILogger<MigrationBatchExecutor> logger) : IMigrationBatchExecutor
{
    /// <summary>
    /// Processes one batch and returns the next command, or <c>null</c> when the cycle is complete.
    /// </summary>
    public async Task<RunMigrationBatchCommand?> ExecuteBatchAsync(
        RunMigrationBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        MigrationCycleRegistration? registration = registry.Find(command.CycleId);
        if (registration is null)
        {
            LogCycleNotFound(command.CycleId);
            return null;
        }

        var tenantContext =
            (DbContext)serviceProvider.GetRequiredService(registration.DbContextType);

        Guid? tenantId = command.TenantId == Guid.Empty ? null : command.TenantId;

        if (tenantId.HasValue)
        {
            await isolator.IsolateAsync(tenantContext, tenantId.Value, cancellationToken).ConfigureAwait(false);
        }

        MigrationProgress progress = await FindOrCreateProgressAsync(command, tenantId, cancellationToken).ConfigureAwait(false);

        if (progress.Status == MigrationStatus.Completed)
        {
            LogCycleAlreadyCompleted(command.CycleId, tenantId);
            return null;
        }

        MigrationBatchContext batchContext = new(command.Cursor, command.BatchSize, command.TenantId);
        MigrationBatchResult result;

        // Bound the user-supplied batch delegate with BatchExecutionTimeout so a delegate
        // that never returns fails the batch instead of hanging the message handler forever.
        TimeSpan timeout = options.Value.BatchExecutionTimeout;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            result = await registration.Migration(tenantContext, batchContext, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            progress.Status = MigrationStatus.Failed;
            progress.Error = $"Batch execution timed out after {timeout}.";
            await SaveProgressAsync(command.CycleId, tenantId, cancellationToken).ConfigureAwait(false);

            LogBatchTimedOut(command.CycleId, tenantId, timeout);
            throw new TimeoutException(
                $"Migration batch for cycle '{command.CycleId}' timed out after {timeout}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress.Status = MigrationStatus.Failed;
            progress.Error = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            await SaveProgressAsync(command.CycleId, tenantId, cancellationToken).ConfigureAwait(false);

            throw;
        }

        progress.ProcessedRows += result.ProcessedCount;
        progress.LastCursor = result.NextCursor;

        if (result.NextCursor is null)
        {
            progress.Status = MigrationStatus.Completed;
            progress.CompletedAt = clock.Now;
            LogCycleCompleted(command.CycleId, tenantId, progress.ProcessedRows);
        }
        else
        {
            LogBatchProcessed(command.CycleId, tenantId, result.ProcessedCount, result.NextCursor);
        }

        await SaveProgressAsync(command.CycleId, tenantId, cancellationToken).ConfigureAwait(false);

        return result.NextCursor is null
            ? null
            : new RunMigrationBatchCommand(command.CycleId, command.TenantId, result.NextCursor, command.BatchSize);
    }

    private async Task<MigrationProgress> FindOrCreateProgressAsync(
        RunMigrationBatchCommand command,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        MigrationProgress? existing = await progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == command.CycleId && p.TenantId == tenantId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        MigrationProgress created = new()
        {
            Id = guidGenerator.Create(),
            CycleId = command.CycleId,
            Phase = MigrationPhase.Migrate,
            Status = MigrationStatus.InProgress,
            TenantId = tenantId,
            StartedAt = clock.Now,
        };

        progressContext.MigrationProgresses.Add(created);
        return created;
    }

    private async Task SaveProgressAsync(
        string cycleId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            await progressContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort: progress tracking must not fail the migration batch.
            LogProgressPersistenceFailed(ex, cycleId, tenantId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration cycle '{CycleId}' not found in registry. Message discarded.")]
    private partial void LogCycleNotFound(string cycleId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migration cycle '{CycleId}' already completed for tenant {TenantId}. Message discarded.")]
    private partial void LogCycleAlreadyCompleted(string cycleId, Guid? tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migration cycle '{CycleId}' completed for tenant {TenantId}. Total rows migrated: {ProcessedRows}.")]
    private partial void LogCycleCompleted(string cycleId, Guid? tenantId, long processedRows);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migration batch processed for cycle '{CycleId}', tenant {TenantId}. Rows this batch: {Count}. Next cursor: '{Cursor}'.")]
    private partial void LogBatchProcessed(string cycleId, Guid? tenantId, int count, string cursor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to persist migration progress for cycle '{CycleId}', tenant {TenantId}. Progress tracking may be stale.")]
    private partial void LogProgressPersistenceFailed(Exception exception, string cycleId, Guid? tenantId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Migration batch for cycle '{CycleId}', tenant {TenantId} timed out after {Timeout}. Cycle marked Failed.")]
    private partial void LogBatchTimedOut(string cycleId, Guid? tenantId, TimeSpan timeout);
}
