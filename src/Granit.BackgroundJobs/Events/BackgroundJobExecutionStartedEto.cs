using Granit.Events;

namespace Granit.BackgroundJobs.Events;

/// <summary>
/// Published when a background job execution starts.
/// Enables real-time monitoring dashboards.
/// </summary>
/// <param name="JobId">The unique identifier of the job definition.</param>
/// <param name="JobName">Stable job name for identification.</param>
/// <param name="StartedAt">Timestamp of the execution start.</param>
public sealed record BackgroundJobExecutionStartedEto(
    Guid JobId,
    string JobName,
    DateTimeOffset StartedAt) : IIntegrationEvent;
