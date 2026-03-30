using System.Threading.Channels;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Background service that reads <see cref="RunMigrationBatchCommand"/> from the channel
/// and executes them via <see cref="MigrationBatchExecutor"/>, cascading until completion.
/// </summary>
/// <remarks>
/// <para>
/// <b>Graceful shutdown</b>: when Kubernetes sends SIGTERM, <c>stoppingToken</c>
/// is cancelled. The worker finishes the <b>current batch</b> before exiting the cascade
/// loop — it never interrupts a batch mid-execution, preventing partial migrations that
/// would leave the database in an inconsistent state.
/// </para>
/// <para>
/// Between batches, <see cref="MigrationStartupOptions.BatchExecutionTimeout"/> provides
/// a safety net against infinite hangs.
/// </para>
/// <para>
/// Progress is persisted after each batch. On next startup, <see cref="MigrationStartupService"/>
/// resumes any <see cref="MigrationStatus.InProgress"/> cycles from their last cursor.
/// </para>
/// </remarks>
internal sealed partial class MigrationBatchWorker(
    Channel<RunMigrationBatchCommand> channel,
    IServiceScopeFactory scopeFactory,
    IOptions<MigrationStartupOptions> options,
    ILogger<MigrationBatchWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted();

        await foreach (RunMigrationBatchCommand initial in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessCascadeAsync(initial, stoppingToken).ConfigureAwait(false);
        }

        LogWorkerStopped();
    }

    private async Task ProcessCascadeAsync(
        RunMigrationBatchCommand initial,
        CancellationToken stoppingToken)
    {
        RunMigrationBatchCommand? command = initial;

        while (command is not null)
        {
            // Each batch gets its own DI scope (Scoped DbContext, isolator, etc.).
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            MigrationBatchExecutor executor = scope.ServiceProvider.GetRequiredService<MigrationBatchExecutor>();

            RunMigrationBatchCommand? next;

            try
            {
                // Safety timeout: prevents infinite hangs on a single batch.
                // Do NOT pass stoppingToken — the in-flight batch must finish to avoid
                // leaving the database in a partially migrated state.
                using CancellationTokenSource batchTimeout = new(options.Value.BatchExecutionTimeout);
                next = await executor.ExecuteBatchAsync(command, batchTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                LogShutdownDuringBatch(command.CycleId);
                return;
            }
            catch (OperationCanceledException)
            {
                // BatchExecutionTimeout expired — not a shutdown, but a safety timeout.
                LogBatchTimeout(command.CycleId, options.Value.BatchExecutionTimeout);
                return;
            }
            catch (Exception ex)
            {
                LogBatchFailed(command.CycleId, ex);
                return;
            }

            // Between batches: check if shutdown was requested.
            if (stoppingToken.IsCancellationRequested)
            {
                LogShutdownBetweenBatches(command.CycleId);
                return;
            }

            command = next;
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migration batch worker started.")]
    private partial void LogWorkerStarted();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migration batch worker stopped.")]
    private partial void LogWorkerStopped();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Shutdown requested during batch execution for cycle '{CycleId}'. "
            + "Progress saved, will resume on next startup.")]
    private partial void LogShutdownDuringBatch(string cycleId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Shutdown requested between batches for cycle '{CycleId}'. "
            + "Remaining cascades deferred to next startup.")]
    private partial void LogShutdownBetweenBatches(string cycleId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Batch execution timeout ({Timeout}) exceeded for cycle '{CycleId}'. "
            + "Batch aborted — progress may be stale.")]
    private partial void LogBatchTimeout(string cycleId, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Migration batch failed for cycle '{CycleId}'. Cascade stopped.")]
    private partial void LogBatchFailed(string cycleId, Exception ex);
}
