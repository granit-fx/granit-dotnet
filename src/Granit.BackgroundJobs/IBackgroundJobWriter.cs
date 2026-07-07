using Granit.BackgroundJobs.Domain;
namespace Granit.BackgroundJobs;

/// <summary>
/// Write operations for administrative control of Granit recurring background jobs.
/// </summary>
/// <remarks>
/// All write operations (<see cref="PauseAsync"/>, <see cref="ResumeAsync"/>,
/// <see cref="TriggerNowAsync"/>) are persisted in the job store and survive application
/// restarts when the durable EF Core store
/// (<c>Granit.BackgroundJobs.EntityFrameworkCore</c>) is used.
/// <para>
/// ISO 27001 compliance: <see cref="TriggerNowAsync"/> propagates the caller's identity
/// via the <c>X-Triggered-By</c> Wolverine envelope header, which is persisted
/// in <see cref="BackgroundJobDefinition.TriggeredBy"/> by the scheduling middleware.
/// </para>
/// </remarks>
public interface IBackgroundJobWriter
{
    /// <summary>
    /// Pauses a recurring job. The current execution (if running) completes normally,
    /// but rescheduling is skipped. The pause state persists across restarts.
    /// </summary>
    /// <exception cref="Granit.Exceptions.EntityNotFoundException">
    /// Thrown when no job with <paramref name="jobName"/> exists in the store.
    /// </exception>
    Task PauseAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused job and immediately schedules its next occurrence
    /// based on the current time and the job's cron expression.
    /// </summary>
    /// <exception cref="Granit.Exceptions.EntityNotFoundException">
    /// Thrown when no job with <paramref name="jobName"/> exists in the store.
    /// </exception>
    Task ResumeAsync(string jobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an immediate execution of a job, independent of its scheduled cycle.
    /// The next scheduled execution is not affected.
    /// The caller's identity (<see cref="Granit.Users.ICurrentUserService.UserId"/>)
    /// is propagated for ISO 27001 audit trail.
    /// </summary>
    /// <exception cref="Granit.Exceptions.EntityNotFoundException">
    /// Thrown when no job with <paramref name="jobName"/> exists in the store.
    /// </exception>
    Task TriggerNowAsync(string jobName, CancellationToken cancellationToken = default);
}
