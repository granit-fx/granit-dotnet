using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Messages;

namespace Granit.DataExchange.Handlers;

/// <summary>
/// Executes an export job via <see cref="IExportOrchestrator"/> — thin adapter between
/// the messaging pipeline and the transport-agnostic orchestrator.
/// </summary>
/// <remarks>
/// Export jobs do not cascade — each command is a self-contained unit of work.
/// </remarks>
public sealed class ExecuteExportCommandHandler
{
    public static Task HandleAsync(
        ExecuteExportCommand command,
        IExportOrchestrator orchestrator,
        CancellationToken cancellationToken) =>
        orchestrator.ExecuteAsync(command.ExportJobId, cancellationToken);
}
