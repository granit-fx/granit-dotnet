namespace Granit.Scheduling.Domain;

/// <summary>
/// Lifecycle status of a <see cref="ScheduledAction"/>.
/// </summary>
public enum ScheduledActionStatus
{
    /// <summary>
    /// The action is waiting for its scheduled execution time.
    /// </summary>
    Pending,

    /// <summary>
    /// The action has been executed successfully.
    /// </summary>
    Executed,

    /// <summary>
    /// The action was cancelled before execution.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The action failed during execution (handler threw after exhausting retries).
    /// </summary>
    Failed,

    /// <summary>
    /// The action has been claimed for execution and is currently being processed.
    /// Transitional status that prevents double-execution via concurrent dispatch.
    /// </summary>
    Processing,
}
