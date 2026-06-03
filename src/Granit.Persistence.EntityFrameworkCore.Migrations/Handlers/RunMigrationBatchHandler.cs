using System.Diagnostics.CodeAnalysis;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Handlers;

/// <summary>
/// Executes one migration batch via <see cref="IMigrationBatchExecutor"/> and cascades the
/// follow-up command while rows remain to be processed.
/// </summary>
/// <remarks>
/// The follow-up command is returned as a Wolverine cascading message, so it is enrolled in
/// the same handler transaction/outbox as the batch execution (no separate <c>IMessageBus</c>
/// dispatch). The cascade stops when the executor returns <c>null</c> (no more rows) —
/// Wolverine publishes nothing for a <c>null</c> return.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class RunMigrationBatchHandler
{
    public static async Task<RunMigrationBatchCommand?> HandleAsync(
        RunMigrationBatchCommand command,
        IMigrationBatchExecutor executor,
        CancellationToken cancellationToken)
        => await executor.ExecuteBatchAsync(command, cancellationToken).ConfigureAwait(false);
}
