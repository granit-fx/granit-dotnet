using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Messages;
using Granit.Wolverine;

namespace Granit.DataExchange.Wolverine.Internal;

/// <summary>
/// <see cref="IExportCommandDispatcher"/> implementation that dispatches export commands
/// via Wolverine's Outbox-backed durable execution.
/// </summary>
internal sealed class WolverineExportCommandDispatcher(
    WolverineScopedSender sender) : IExportCommandDispatcher
{
    /// <inheritdoc/>
    public Task DispatchAsync(ExecuteExportCommand command, CancellationToken cancellationToken = default) =>
        sender.SendAsync(command);
}
