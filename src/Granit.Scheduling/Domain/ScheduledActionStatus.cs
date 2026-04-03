namespace Granit.Scheduling.Domain;

/// <summary>
/// Lifecycle status of a <see cref="ScheduledAction"/>.
/// </summary>
public enum ScheduledActionStatus
{
    /// <summary>
    /// The action is waiting for its scheduled execution time.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The action has been executed successfully.
    /// </summary>
    Executed = 1,

    /// <summary>
    /// The action was cancelled before execution.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// The action failed during execution (handler threw after exhausting retries).
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The action has been claimed for execution and is currently being processed.
    /// Transitional status that prevents double-execution via concurrent dispatch.
    /// </summary>
    Processing = 4,
}
