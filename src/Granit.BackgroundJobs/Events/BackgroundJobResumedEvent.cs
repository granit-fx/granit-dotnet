using Granit.Events;

namespace Granit.BackgroundJobs.Events;

/// <summary>
/// Raised when a paused background job is resumed by an administrator.
/// </summary>
public sealed record BackgroundJobResumedEvent(
    Guid JobId,
    string JobName) : IDomainEvent;
