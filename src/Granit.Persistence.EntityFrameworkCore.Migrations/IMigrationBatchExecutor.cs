using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;

namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Executes a single migration batch and returns the next command (cascade), or <c>null</c>
/// when all rows for the cycle are processed.
/// </summary>
/// <remarks>
/// Consumed by <see cref="Handlers.RunMigrationBatchHandler"/> to advance the Expand &amp;
/// Contract pipeline one batch at a time.
/// </remarks>
public interface IMigrationBatchExecutor
{
    /// <summary>
    /// Executes one batch for the given cycle and tenant. Returns the next batch command
    /// when work remains, or <c>null</c> when the cycle is complete.
    /// </summary>
    Task<RunMigrationBatchCommand?> ExecuteBatchAsync(
        RunMigrationBatchCommand command,
        CancellationToken cancellationToken = default);
}
