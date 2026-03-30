using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations;

#pragma warning disable CA1711 // The 'Delegate' suffix is intentional — this IS a delegate type.

/// <summary>
/// Delegate executed for each batch during the <see cref="MigrationPhase.Migrate"/> phase.
/// </summary>
/// <param name="context">
/// The tenant-isolated <see cref="DbContext"/> to use for data access.
/// Already scoped to the correct tenant by <see cref="ITenantDbIsolator"/>.
/// </param>
/// <param name="batch">Contextual information about the current batch.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>
/// A <see cref="MigrationBatchResult"/> containing the number of rows processed and
/// the cursor for the next batch, or <c>null</c> for the next cursor when finished.
/// </returns>
/// <remarks>
/// Implementations MUST be idempotent: re-running a batch over already-migrated rows must
/// produce no side effects. Use a condition such as <c>WHERE new_column IS NULL</c> to
/// skip rows that have already been processed.
/// </remarks>
public delegate Task<MigrationBatchResult> BatchMigrationDelegate(
    DbContext context,
    MigrationBatchContext batch,
    CancellationToken cancellationToken);
