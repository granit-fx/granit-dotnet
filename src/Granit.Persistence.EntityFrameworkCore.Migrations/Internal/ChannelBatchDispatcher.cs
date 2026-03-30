using System.Threading.Channels;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Default <see cref="IMigrationBatchDispatcher"/> implementation that writes commands
/// to an unbounded <see cref="Channel{T}"/> consumed by <see cref="MigrationBatchWorker"/>.
/// </summary>
/// <remarks>
/// Registered as a Singleton. The channel is shared with <see cref="MigrationBatchWorker"/>
/// via constructor injection.
/// </remarks>
internal sealed class ChannelBatchDispatcher(
    Channel<RunMigrationBatchCommand> channel) : IMigrationBatchDispatcher
{
    /// <inheritdoc/>
    public async Task DispatchAsync(RunMigrationBatchCommand command, CancellationToken cancellationToken = default) =>
        await channel.Writer.WriteAsync(command, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task DispatchAsync(IEnumerable<RunMigrationBatchCommand> commands, CancellationToken cancellationToken = default)
    {
        foreach (RunMigrationBatchCommand command in commands)
        {
            await channel.Writer.WriteAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }
}
