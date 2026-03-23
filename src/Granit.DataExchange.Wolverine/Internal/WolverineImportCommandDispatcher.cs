using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Pipeline;
using Granit.Wolverine;

namespace Granit.DataExchange.Wolverine.Internal;

/// <summary>
/// <see cref="IImportCommandDispatcher"/> implementation that dispatches import commands
/// via Wolverine's Outbox-backed durable execution.
/// </summary>
internal sealed class WolverineImportCommandDispatcher(
    WolverineScopedSender sender) : IImportCommandDispatcher
{
    /// <inheritdoc/>
    public Task DispatchAsync(ExecuteImportCommand command, CancellationToken cancellationToken = default) =>
        sender.SendAsync(command);
}
