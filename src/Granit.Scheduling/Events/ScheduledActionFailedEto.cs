using Granit.Events;

namespace Granit.Scheduling.Events;

/// <summary>
/// Published when a scheduled action fails after exhausting retries.
/// Enables monitoring and alerting on failed scheduled operations.
/// </summary>
/// <param name="ActionId">The scheduled action identifier.</param>
/// <param name="PayloadType">The CLR type name of the failed payload.</param>
/// <param name="CorrelationId">The optional correlation identifier linking to a domain entity.</param>
/// <param name="FailureReason">Truncated error message (max 500 characters).</param>
/// <param name="FailedAt">The UTC timestamp when the failure was recorded.</param>
public sealed record ScheduledActionFailedEto(
    Guid ActionId,
    string PayloadType,
    string? CorrelationId,
    string FailureReason,
    DateTimeOffset FailedAt) : IIntegrationEvent;
