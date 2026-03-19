using Granit.Core.Events;

namespace Granit.BackgroundJobs.Events;

/// <summary>
/// Published when a background job reaches 3 consecutive failures, signaling potential
/// infrastructure or configuration issues that require operator attention.
/// </summary>
public sealed record BackgroundJobFailureThresholdExceededEto(
    Guid JobId,
    string JobName,
    int ConsecutiveFailureCount,
    string? LastErrorMessage) : IIntegrationEvent;
