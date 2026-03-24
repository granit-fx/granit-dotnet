using Granit.Events;

namespace Granit.BackgroundJobs.Events;

/// <summary>
/// Raised when a background job is paused by an administrator.
/// </summary>
public sealed record BackgroundJobPausedEvent(
    Guid JobId,
    string JobName) : IDomainEvent;
