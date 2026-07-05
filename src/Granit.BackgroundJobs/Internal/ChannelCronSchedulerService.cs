using Cronos;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Domain;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Background service that initializes recurring job scheduling on startup.
/// </summary>
/// <remarks>
/// <para>
/// This is the default in-process scheduler. It iterates all enabled jobs and schedules
/// the first occurrence via <see cref="IBackgroundJobDispatcher"/>. Unlike the Wolverine
/// <c>SingularAgent</c>, this does not guarantee cluster-level singleton execution — on
/// multi-node deployments, use <c>Granit.BackgroundJobs.Wolverine</c> instead.
/// </para>
/// <para>
/// Anti-doublon: before scheduling a job, checks whether
/// <see cref="BackgroundJobDefinition.NextExecutionAt"/> is already in the future.
/// </para>
/// </remarks>
internal sealed partial class ChannelCronSchedulerService(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    IHostEnvironment environment,
    ILogger<ChannelCronSchedulerService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!environment.IsDevelopment())
        {
            LogMultiReplicaWarning();
        }

        // Small delay to let seed service run first.
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IBackgroundJobStoreReader storeReader =
            scope.ServiceProvider.GetRequiredService<IBackgroundJobStoreReader>();
        IBackgroundJobStoreWriter storeWriter =
            scope.ServiceProvider.GetRequiredService<IBackgroundJobStoreWriter>();
        IBackgroundJobDispatcher dispatcher =
            scope.ServiceProvider.GetRequiredService<IBackgroundJobDispatcher>();

        IReadOnlyList<BackgroundJobDefinition> jobs =
            await storeReader.GetEnabledJobsAsync(stoppingToken).ConfigureAwait(false);

        foreach (BackgroundJobDefinition job in jobs)
        {
            if (job.NextExecutionAt > clock.Now)
            {
                LogJobAlreadyScheduled(job.JobName, job.NextExecutionAt.Value);
                continue;
            }

            DateTimeOffset? next = ComputeNext(job.CronExpression);
            if (next is null)
            {
                LogJobCronInvalid(job.JobName, job.CronExpression);
                continue;
            }

            object message = CronSchedulerHelper.CreateMessage(job.MessageType, job.JobName);
            await dispatcher.ScheduleAsync(message, next.Value, stoppingToken).ConfigureAwait(false);
            await storeWriter.RecordNextExecutionAsync(job.JobName, next.Value, stoppingToken)
                .ConfigureAwait(false);
            LogJobScheduled(job.JobName, next.Value);
        }
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

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "BackgroundJob '{JobName}' already scheduled for {Next} — skipping")]
    private partial void LogJobAlreadyScheduled(string jobName, DateTimeOffset next);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BackgroundJob '{JobName}' has invalid cron '{Cron}' — cannot schedule")]
    private partial void LogJobCronInvalid(string jobName, string cron);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "BackgroundJob '{JobName}' scheduled for first occurrence at {Next}")]
    private partial void LogJobScheduled(string jobName, DateTimeOffset next);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Using the in-process scheduler (Granit.BackgroundJobs without Wolverine) in a non-Development environment. " +
                  "Recurring jobs may fire on every replica and scheduled state is not persisted across pod restarts. " +
                  "Add Granit.BackgroundJobs.Wolverine for cluster-safe, durable scheduling.")]
    private partial void LogMultiReplicaWarning();
}
