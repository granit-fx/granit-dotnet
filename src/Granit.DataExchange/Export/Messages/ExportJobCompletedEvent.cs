using Granit.Core.Events;

namespace Granit.DataExchange.Export.Messages;

/// <summary>
/// Integration event published when an export job reaches a terminal state
/// (<see cref="ExportJobStatus.Completed"/> or <see cref="ExportJobStatus.Failed"/>).
/// </summary>
/// <remarks>
/// Consumed by notification handlers, audit loggers, etc.
/// No PII — only identifiers and aggregate counts (ISO 27001-compliant).
/// </remarks>
/// <param name="ExportJobId">The export job identifier.</param>
/// <param name="DefinitionName">The export definition name.</param>
/// <param name="Status">Terminal status of the job.</param>
/// <param name="UserId">Identifier of the user who created the job.</param>
/// <param name="RowCount">Number of rows exported (null if failed before counting).</param>
/// <param name="ErrorMessage">Error message if failed (null if completed).</param>
public sealed record ExportJobCompletedEto(
    Guid ExportJobId,
    string DefinitionName,
    ExportJobStatus Status,
    string UserId,
    int? RowCount,
    string? ErrorMessage) : IIntegrationEvent;
