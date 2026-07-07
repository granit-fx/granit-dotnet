using Granit.BackgroundJobs.Events;
using Granit.Domain;

namespace Granit.BackgroundJobs.Domain;

/// <summary>
/// Persistent administrative record for a Wolverine recurring job.
/// </summary>
/// <remarks>
/// <para>
/// This entity tracks the administrative state of a job — not the Wolverine messages themselves
/// (those live in the Outbox). It stores scheduling metadata, execution history, and pause/resume
/// state that persists across application restarts.
/// </para>
/// <para>
/// ISO 27001 compliance: <see cref="LastExecutedAt"/> and <see cref="TriggeredBy"/> are write-once
/// per execution cycle and preserved for audit purposes.
/// </para>
/// </remarks>
public sealed class BackgroundJobDefinition : AggregateRoot
{
    /// <summary>
    /// Maximum length of stored error messages. Prevents unbounded PII propagation
    /// through API responses and integration events.
    /// </summary>
    internal const int MaxErrorMessageLength = 500;

    // Parameterless constructor required by EF Core materializer.
    private BackgroundJobDefinition() { }

    /// <summary>
    /// Creates a new <see cref="BackgroundJobDefinition"/> in enabled state.
    /// </summary>
    public static BackgroundJobDefinition Create(
        Guid id,
        string jobName,
        string cronExpression,
        string messageType) => new()
        {
            Id = id,
            JobName = jobName,
            CronExpression = cronExpression,
            MessageType = messageType,
            IsEnabled = true,
        };

    /// <summary>
    /// Unique, stable job name matching <see cref="RecurringJobAttribute.Name"/>.
    /// Primary lookup key. Maximum length: 200 characters.
    /// </summary>
    public string JobName { get; private set; } = string.Empty;

    /// <summary>
    /// Assembly-qualified CLR type name of the Wolverine message.
    /// Used by <see cref="IBackgroundJobWriter.TriggerNowAsync"/> to instantiate the message.
    /// Maximum length: 500 characters.
    /// </summary>
    public string MessageType { get; private set; } = string.Empty;

    /// <summary>
    /// Cron expression (5 or 6 fields) defining the recurrence schedule.
    /// Updated on each deployment if the expression changes in code.
    /// Maximum length: 100 characters.
    /// </summary>
    public string CronExpression { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the job is active. When <c>false</c>, the scheduling middleware
    /// skips rescheduling after each execution (Pause).
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>UTC timestamp of the last successful execution start.</summary>
    public DateTimeOffset? LastExecutedAt { get; private set; }

    /// <summary>UTC timestamp of the next scheduled execution (set by the middleware).</summary>
    public DateTimeOffset? NextExecutionAt { get; private set; }

    /// <summary>
    /// Number of consecutive handler failures since the last successful execution.
    /// Reset to zero on success.
    /// </summary>
    public int ConsecutiveFailureCount { get; private set; }

    /// <summary>
    /// Error message from the last handler failure, truncated to
    /// <see cref="MaxErrorMessageLength"/> characters. Null when the last execution succeeded.
    /// </summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>
    /// UserId (not PII) of the operator who manually triggered this job via
    /// <see cref="IBackgroundJobWriter.TriggerNowAsync"/>.
    /// Null for scheduled executions. ISO 27001 audit field.
    /// Maximum length: 450 characters.
    /// </summary>
    public string? TriggeredBy { get; private set; }

    /// <summary>
    /// Updates the schedule and message type when the code-declared definition changes on deployment.
    /// </summary>
    internal void UpdateDefinition(string cronExpression, string messageType)
    {
        string oldCron = CronExpression;
        CronExpression = cronExpression;
        MessageType = messageType;

        if (!string.Equals(oldCron, cronExpression, StringComparison.Ordinal))
        {
            AddDomainEvent(new BackgroundJobDefinitionChangedEvent(Id, JobName, oldCron, cronExpression));
        }
    }

    /// <summary>
    /// Records the start of a successful execution. Resets failure counters.
    /// </summary>
    internal void RecordExecutionStart(DateTimeOffset startedAt)
    {
        LastExecutedAt = startedAt;
        LastErrorMessage = null;
        ConsecutiveFailureCount = 0;
        TriggeredBy = null;
        AddDistributedEvent(new BackgroundJobExecutionStartedEto(Id, JobName, startedAt));
    }

    /// <summary>
    /// Schedules the next execution time.
    /// </summary>
    internal void ScheduleNext(DateTimeOffset? nextExecution) =>
        NextExecutionAt = nextExecution;

    /// <summary>
    /// Records an execution failure. Error messages are truncated to
    /// <see cref="MaxErrorMessageLength"/> characters to limit information disclosure.
    /// A <see cref="BackgroundJobFailureThresholdExceededEto"/> is published exactly once,
    /// when the consecutive failure count reaches <paramref name="failureAlertThreshold"/> —
    /// subsequent failures do not re-alert until a successful execution resets the counter.
    /// </summary>
    internal void RecordFailure(string? errorMessage, int failureAlertThreshold)
    {
        ConsecutiveFailureCount++;
        LastErrorMessage = errorMessage?.Length > MaxErrorMessageLength
            ? $"{errorMessage.AsSpan(0, MaxErrorMessageLength)}… [truncated]"
            : errorMessage;

        if (ConsecutiveFailureCount == failureAlertThreshold)
        {
            AddDistributedEvent(new BackgroundJobFailureThresholdExceededEto(
                Id, JobName, ConsecutiveFailureCount, LastErrorMessage));
        }
    }

    /// <summary>
    /// Sets the UserId of the operator who manually triggered this job.
    /// </summary>
    internal void SetTriggeredBy(string? triggeredBy) =>
        TriggeredBy = triggeredBy;

    /// <summary>
    /// Pauses the job and emits a <see cref="BackgroundJobPausedEvent"/> domain event.
    /// </summary>
    internal void Pause()
    {
        IsEnabled = false;
        AddDomainEvent(new BackgroundJobPausedEvent(Id, JobName));
    }

    /// <summary>
    /// Resumes the job and emits a <see cref="BackgroundJobResumedEvent"/> domain event.
    /// </summary>
    internal void Resume()
    {
        IsEnabled = true;
        AddDomainEvent(new BackgroundJobResumedEvent(Id, JobName));
    }
}
