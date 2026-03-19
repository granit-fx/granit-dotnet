using Granit.Core.Events;

namespace Granit.DataExchange.Export.Events;

/// <summary>
/// Published when an export job fails.
/// Enables cross-service error alerting and monitoring.
/// </summary>
/// <param name="ExportJobId">The unique identifier of the failed job.</param>
/// <param name="DefinitionName">The export definition name.</param>
/// <param name="ErrorMessage">Error message from the failure.</param>
public sealed record ExportJobFailedEto(
    Guid ExportJobId,
    string DefinitionName,
    string ErrorMessage) : IIntegrationEvent;
