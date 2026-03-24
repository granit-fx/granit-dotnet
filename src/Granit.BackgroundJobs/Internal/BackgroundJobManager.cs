using System.Diagnostics;
using Cronos;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Diagnostics;
using Granit.BackgroundJobs.Domain;
using Granit.Exceptions;
using Granit.Timing;
using Granit.Users;
using Microsoft.Extensions.Logging;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Default implementation of <see cref="IBackgroundJobReader"/> and <see cref="IBackgroundJobWriter"/>.
/// Registered as <b>Scoped</b> in the DI container.
/// </summary>
internal sealed partial class BackgroundJobManager(
    IBackgroundJobStoreReader storeReader,
    IBackgroundJobStoreWriter storeWriter,
    IBackgroundJobDispatcher dispatcher,
    IDeadLetterQueueInspector dlqInspector,
    IClock clock,
    ICurrentUserService currentUserService,
    ILogger<BackgroundJobManager> logger) : IBackgroundJobReader, IBackgroundJobWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<BackgroundJobStatus>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BackgroundJobDefinition> jobs = await storeReader.GetAllJobsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, long> dlqCounts = await dlqInspector.GetCountsAsync(cancellationToken).ConfigureAwait(false);
        return jobs.Select(j => ToStatus(j, dlqCounts)).ToList();
    }

    /// <inheritdoc/>
    public async Task<BackgroundJobStatus?> FindAsync(string jobName, CancellationToken cancellationToken = default)
    {
        BackgroundJobDefinition? job = await storeReader.FindAsync(jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        IReadOnlyDictionary<string, long> dlqCounts = await dlqInspector.GetCountsAsync(cancellationToken).ConfigureAwait(false);
        return ToStatus(job, dlqCounts);
    }

    /// <inheritdoc/>
    public async Task PauseAsync(string jobName, CancellationToken cancellationToken = default)
    {
        BackgroundJobDefinition job = await RequireJobAsync(jobName, cancellationToken).ConfigureAwait(false);
        await storeWriter.SetEnabledAsync(job.JobName, false, cancellationToken).ConfigureAwait(false);
        LogJobPaused(logger, jobName);
    }

    /// <inheritdoc/>
    public async Task ResumeAsync(string jobName, CancellationToken cancellationToken = default)
    {
        BackgroundJobDefinition job = await RequireJobAsync(jobName, cancellationToken).ConfigureAwait(false);
        await storeWriter.SetEnabledAsync(job.JobName, true, cancellationToken).ConfigureAwait(false);

        DateTimeOffset? next = ComputeNext(job.CronExpression);
        if (next is not null)
        {
            object message = CronSchedulerHelper.CreateMessage(job.MessageType, jobName);
            await dispatcher.ScheduleAsync(message, next.Value, cancellationToken).ConfigureAwait(false);
            await storeWriter.RecordNextExecutionAsync(job.JobName, next.Value, cancellationToken).ConfigureAwait(false);
            LogJobResumed(logger, jobName, next.Value);
        }
        else
        {
            LogJobResumedNoCron(logger, jobName, job.CronExpression);
        }
    }

    /// <inheritdoc/>
    public async Task TriggerNowAsync(string jobName, CancellationToken cancellationToken = default)
    {
        BackgroundJobDefinition job = await RequireJobAsync(jobName, cancellationToken).ConfigureAwait(false);

        using Activity? activity = BackgroundJobsActivitySource.Source.StartActivity(BackgroundJobsActivitySource.Trigger);
        activity?.SetTag("backgroundjobs.job_name", jobName);
        activity?.SetTag("backgroundjobs.triggered_by", currentUserService.UserId ?? "system");

        object message = CronSchedulerHelper.CreateMessage(job.MessageType, jobName);

        Dictionary<string, string>? headers = null;
        if (currentUserService.IsAuthenticated
            && currentUserService.UserId is { Length: > 0 } userId)
        {
            headers = new() { [BackgroundJobHeaders.TriggeredBy] = userId };
        }

        await dispatcher.PublishAsync(message, headers, cancellationToken).ConfigureAwait(false);
        LogJobTriggered(logger, jobName, currentUserService.UserId ?? "system");
    }

    private async Task<BackgroundJobDefinition> RequireJobAsync(
        string jobName,
        CancellationToken cancellationToken)
    {
        BackgroundJobDefinition? job = await storeReader.FindAsync(jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            throw new EntityNotFoundException(typeof(BackgroundJobDefinition), jobName);
        }

        return job;
    }

    private static BackgroundJobStatus ToStatus(
        BackgroundJobDefinition job,
        IReadOnlyDictionary<string, long> dlqCounts)
    {
        // BackgroundJobDefinition.MessageType is assembly-qualified (e.g. "MyApp.MyMsg, MyApp, ...").
        // DeadLetterQueueCount.MessageType contains only the type's full name (no assembly suffix).
        string shortTypeName = job.MessageType.Split(',')[0].Trim();
        long dlqCount = dlqCounts.TryGetValue(shortTypeName, out long count) ? count : 0;

        return new(
            JobName: job.JobName,
            CronExpression: job.CronExpression,
            IsEnabled: job.IsEnabled,
            LastExecutedAt: job.LastExecutedAt,
            NextExecutionAt: job.NextExecutionAt,
            ConsecutiveFailures: job.ConsecutiveFailureCount,
            DeadLetterCount: dlqCount,
            LastError: job.LastErrorMessage);
    }

    private DateTimeOffset? ComputeNext(string cronExpression)
    {
        try
        {
            CronExpression cron;
            try
            {
                cron = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
            }
            catch (CronFormatException)
            {
                cron = CronExpression.Parse(cronExpression);
            }

            return cron.GetNextOccurrence(clock.Now, TimeZoneInfo.Utc);
        }
        catch (CronFormatException)
        {
            return null;
        }
    }

    // =========================================================================
    // Source-generated logger messages (CA1873 / CA1848 compliance)
    // =========================================================================

    [LoggerMessage(Level = LogLevel.Information, Message = "BackgroundJob '{JobName}' paused.")]
    private static partial void LogJobPaused(ILogger logger, string jobName);

    [LoggerMessage(Level = LogLevel.Information, Message = "BackgroundJob '{JobName}' resumed. Next execution: {Next}.")]
    private static partial void LogJobResumed(ILogger logger, string jobName, DateTimeOffset next);

    [LoggerMessage(Level = LogLevel.Warning, Message = "BackgroundJob '{JobName}' resumed but cron '{Cron}' produced no next occurrence.")]
    private static partial void LogJobResumedNoCron(ILogger logger, string jobName, string cron);

    [LoggerMessage(Level = LogLevel.Information, Message = "BackgroundJob '{JobName}' triggered manually by '{UserId}'.")]
    private static partial void LogJobTriggered(ILogger logger, string jobName, string userId);
}
