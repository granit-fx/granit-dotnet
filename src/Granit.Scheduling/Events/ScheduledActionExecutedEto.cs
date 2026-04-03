using Granit.Events;

namespace Granit.Scheduling.Events;

/// <summary>
/// Published when a scheduled action executes successfully.
/// Enables cross-module reactions to completed scheduled operations.
/// </summary>
/// <param name="ActionId">The scheduled action identifier.</param>
/// <param name="PayloadType">The CLR type name of the executed payload.</param>
/// <param name="CorrelationId">The optional correlation identifier linking to a domain entity.</param>
/// <param name="ExecutedAt">The UTC timestamp when execution completed.</param>
public sealed record ScheduledActionExecutedEto(
    Guid ActionId,
    string PayloadType,
    string? CorrelationId,
    DateTimeOffset ExecutedAt) : IIntegrationEvent;
