using Granit.Core.Events;

namespace Granit.BackgroundJobs.Events;

/// <summary>
/// Raised when a background job definition is updated on deployment.
/// Enables deployment audit for schedule modifications.
/// </summary>
/// <param name="JobId">The unique identifier of the job definition.</param>
/// <param name="JobName">Stable job name for identification.</param>
/// <param name="OldCronExpression">Previous cron schedule.</param>
/// <param name="NewCronExpression">Updated cron schedule.</param>
public sealed record BackgroundJobDefinitionChangedEvent(
    Guid JobId,
    string JobName,
    string OldCronExpression,
    string NewCronExpression) : IDomainEvent;
