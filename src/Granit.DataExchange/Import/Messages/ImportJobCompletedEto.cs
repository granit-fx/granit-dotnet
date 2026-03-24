using Granit.DataExchange.Import.Domain;
using Granit.Events;

namespace Granit.DataExchange.Import.Messages;

/// <summary>
/// Integration event published when an import job reaches a terminal state
/// (<see cref="ImportJobStatus.Completed"/>, <see cref="ImportJobStatus.PartiallyCompleted"/>,
/// or <see cref="ImportJobStatus.Failed"/>).
/// </summary>
/// <remarks>
/// Consumed by notification handlers, audit loggers, etc.
/// No PII — only identifiers and aggregate counts (ISO 27001-compliant).
/// </remarks>
/// <param name="ImportJobId">The import job identifier.</param>
/// <param name="DefinitionName">The import definition name.</param>
/// <param name="Status">Terminal status of the job.</param>
/// <param name="UserId">Identifier of the user who created the job.</param>
/// <param name="TotalRows">Total rows processed.</param>
/// <param name="SucceededRows">Rows successfully imported.</param>
/// <param name="FailedRows">Rows that failed.</param>
/// <param name="InsertedRows">New records inserted.</param>
/// <param name="UpdatedRows">Existing records updated.</param>
/// <param name="SkippedRows">Rows skipped (e.g. empty rows).</param>
public sealed record ImportJobCompletedEto(
    Guid ImportJobId,
    string DefinitionName,
    ImportJobStatus Status,
    string UserId,
    int TotalRows,
    int SucceededRows,
    int FailedRows,
    int InsertedRows,
    int UpdatedRows,
    int SkippedRows) : IIntegrationEvent;
