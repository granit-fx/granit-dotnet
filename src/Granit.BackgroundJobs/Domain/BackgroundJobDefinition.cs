using Granit.BackgroundJobs.Events;
using Granit.BackgroundJobs.Internal;
using Granit.Core.Domain;

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
    /// Error message from the last handler failure.
    /// Maximum length: 2000 characters. Null when the last execution succeeded.
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
        CronExpression = cronExpression;
        MessageType = messageType;
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
    }

    /// <summary>
    /// Schedules the next execution time.
    /// </summary>
    internal void ScheduleNext(DateTimeOffset? nextExecution)
    {
        NextExecutionAt = nextExecution;
    }

    /// <summary>
    /// Records an execution failure.
    /// </summary>
    internal void RecordFailure(string? errorMessage)
    {
        ConsecutiveFailureCount++;
        LastErrorMessage = errorMessage;
    }

    /// <summary>
    /// Sets the UserId of the operator who manually triggered this job.
    /// </summary>
    internal void SetTriggeredBy(string? triggeredBy)
    {
        TriggeredBy = triggeredBy;
    }

    /// <summary>
    /// Pauses the job and emits a <see cref="BackgroundJobPaused"/> domain event.
    /// </summary>
    internal void Pause()
    {
        IsEnabled = false;
        AddDomainEvent(new BackgroundJobPaused(Id, JobName));
    }

    /// <summary>
    /// Resumes the job and emits a <see cref="BackgroundJobResumed"/> domain event.
    /// </summary>
    internal void Resume()
    {
        IsEnabled = true;
        AddDomainEvent(new BackgroundJobResumed(Id, JobName));
    }
}
