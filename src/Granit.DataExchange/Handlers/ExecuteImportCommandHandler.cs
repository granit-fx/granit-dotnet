using System.Diagnostics.CodeAnalysis;
using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Import.Pipeline;

namespace Granit.DataExchange.Handlers;

/// <summary>
/// Executes an import job via <see cref="IImportOrchestrator"/> — thin adapter between
/// the messaging pipeline and the transport-agnostic orchestrator.
/// </summary>
/// <remarks>
/// Import jobs do not cascade — each command is a self-contained unit of work.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class ExecuteImportCommandHandler
{
    public static Task HandleAsync(
        ExecuteImportCommand command,
        IImportOrchestrator orchestrator,
        CancellationToken cancellationToken) =>
        orchestrator.ExecuteAsync(command.ImportJobId, cancellationToken);
}
