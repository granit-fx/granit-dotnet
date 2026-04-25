using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class ExecuteExportCommandHandler
{
    public static Task HandleAsync(
        ExecuteExportCommand command,
        IExportOrchestrator orchestrator,
        CancellationToken cancellationToken) =>
        orchestrator.ExecuteAsync(command.ExportJobId, cancellationToken);
}
