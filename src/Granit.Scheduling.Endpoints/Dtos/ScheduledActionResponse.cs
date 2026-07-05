using Granit.Scheduling.Domain;

namespace Granit.Scheduling.Endpoints.Dtos;

/// <summary>
/// Response DTO representing a scheduled action.
/// </summary>
/// <param name="Id">The scheduled action identifier.</param>
/// <param name="PayloadType">The CLR type name of the payload.</param>
/// <param name="ExecuteAt">The UTC date when the action is scheduled to execute.</param>
/// <param name="CorrelationId">Optional correlation identifier linking to a domain entity.</param>
/// <param name="Status">The current lifecycle status.</param>
/// <param name="ExecutedAt">The UTC timestamp when the action was executed (null if not yet).</param>
/// <param name="CancelledBy">The user who cancelled the action (null if not cancelled).</param>
/// <param name="FailureReason">Error message if the action failed.</param>
/// <param name="AttemptCount">Number of execution attempts recorded at the terminal status (0 while Pending/Processing).</param>
/// <param name="CreatedAt">The UTC timestamp when the action was created.</param>
/// <param name="ModifiedAt">The UTC timestamp of the last state change (reschedule, cancellation, execution); <c>null</c> if the action was never modified.</param>
public sealed record ScheduledActionResponse(
    Guid Id,
    string PayloadType,
    DateTimeOffset ExecuteAt,
    string? CorrelationId,
    ScheduledActionStatus Status,
    DateTimeOffset? ExecutedAt,
    string? CancelledBy,
    string? FailureReason,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);
