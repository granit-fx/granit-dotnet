using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Wolverine;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine.Internal;

/// <summary>
/// <see cref="IMigrationBatchDispatcher"/> implementation that dispatches batch commands
/// via Wolverine's Outbox-backed durable execution.
/// </summary>
internal sealed class WolverineMigrationBatchDispatcher(
    WolverineScopedSender sender) : IMigrationBatchDispatcher
{
    /// <inheritdoc/>
    public Task DispatchAsync(RunMigrationBatchCommand command, CancellationToken cancellationToken = default) =>
        sender.SendAsync(command);

    /// <inheritdoc/>
    public async Task DispatchAsync(IEnumerable<RunMigrationBatchCommand> commands, CancellationToken cancellationToken = default)
    {
        foreach (RunMigrationBatchCommand command in commands)
        {
            await sender.SendAsync(command).ConfigureAwait(false);
        }
    }
}
