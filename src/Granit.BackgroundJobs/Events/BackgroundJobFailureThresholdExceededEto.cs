using Granit.Events;

namespace Granit.BackgroundJobs.Events;

/// <summary>
/// Published when consecutive job failures reach the threshold (3).
/// Enables SLA alerting before automatic pause.
/// </summary>
/// <param name="JobId">The unique identifier of the job definition.</param>
/// <param name="JobName">Stable job name for identification.</param>
/// <param name="ConsecutiveFailureCount">Number of consecutive failures.</param>
/// <param name="LastErrorMessage">Error message from the last failure.</param>
public sealed record BackgroundJobFailureThresholdExceededEto(
    Guid JobId,
    string JobName,
    int ConsecutiveFailureCount,
    string? LastErrorMessage) : IIntegrationEvent;
