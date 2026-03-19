using Granit.Core.Events;

namespace Granit.DataExchange.Import.Events;

/// <summary>
/// Raised when an import job is cancelled.
/// Enables audit trail and cleanup workflows.
/// </summary>
/// <param name="ImportJobId">The unique identifier of the cancelled job.</param>
/// <param name="DefinitionName">The import definition name.</param>
public sealed record ImportJobCancelledEvent(
    Guid ImportJobId,
    string DefinitionName) : IDomainEvent;
