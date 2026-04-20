using Granit.Commands;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Handlers;

/// <summary>
/// Executes one migration batch via <see cref="IMigrationBatchExecutor"/> and cascades a
/// follow-up command while rows remain to be processed.
/// </summary>
/// <remarks>
/// The follow-up command is dispatched via <see cref="ICommandSender"/> (provider-agnostic).
/// The cascade stops when the executor returns <c>null</c> (no more rows).
/// </remarks>
public sealed class RunMigrationBatchHandler
{
    public static async Task HandleAsync(
        RunMigrationBatchCommand command,
        IMigrationBatchExecutor executor,
        ICommandSender commandSender,
        CancellationToken cancellationToken)
    {
        RunMigrationBatchCommand? next = await executor
            .ExecuteBatchAsync(command, cancellationToken)
            .ConfigureAwait(false);

        if (next is not null)
        {
            await commandSender.SendAsync(next, cancellationToken).ConfigureAwait(false);
        }
    }
}
