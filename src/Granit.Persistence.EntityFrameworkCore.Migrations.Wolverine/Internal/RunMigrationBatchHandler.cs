using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine.Internal;

/// <summary>
/// Wolverine handler that executes one migration batch via <see cref="MigrationBatchExecutor"/>
/// and cascades a follow-up command while rows remain to be processed.
/// </summary>
/// <remarks>
/// <para>
/// Cascade pattern: returning <c>object[]</c> from a Wolverine handler emits zero or more
/// follow-up messages into the durable Outbox. An empty array stops the cascade.
/// </para>
/// <para>
/// This handler is a thin adapter between Wolverine's cascade pattern and the
/// transport-agnostic <see cref="MigrationBatchExecutor"/>.
/// </para>
/// </remarks>
internal sealed class RunMigrationBatchHandler(MigrationBatchExecutor executor)
{
    /// <summary>
    /// Processes one batch and cascades the next command, or returns empty when done.
    /// </summary>
    public async Task<object[]> HandleAsync(RunMigrationBatchCommand command, CancellationToken cancellationToken)
    {
        RunMigrationBatchCommand? next = await executor.ExecuteBatchAsync(command, cancellationToken).ConfigureAwait(false);
        return next is null ? [] : [next];
    }
}
