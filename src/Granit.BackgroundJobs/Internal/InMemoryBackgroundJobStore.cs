using System.Collections.Concurrent;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Options;
using Granit.Guids;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IBackgroundJobStoreReader"/> and
/// <see cref="IBackgroundJobStoreWriter"/>.
/// Registered as a <b>Singleton</b> when <see cref="BackgroundJobsOptions.Mode"/> is
/// <see cref="JobStoreMode.InMemory"/>. State is lost on application restart.
/// </summary>
internal sealed class InMemoryBackgroundJobStore(IGuidGenerator guidGenerator) : IBackgroundJobStoreReader, IBackgroundJobStoreWriter
{
    private readonly ConcurrentDictionary<string, BackgroundJobDefinition> _jobs = new();

    /// <inheritdoc/>
    public Task<BackgroundJobDefinition?> FindAsync(string jobName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job) ? job : null);

    /// <inheritdoc/>
    public Task<IReadOnlyList<BackgroundJobDefinition>> GetEnabledJobsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackgroundJobDefinition>>(
            _jobs.Values.Where(j => j.IsEnabled).ToList());

    /// <inheritdoc/>
    public Task<IReadOnlyList<BackgroundJobDefinition>> GetAllJobsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BackgroundJobDefinition>>(_jobs.Values.ToList());

    /// <inheritdoc/>
    public Task SeedJobsAsync(IEnumerable<RecurringJobRegistration> registrations, CancellationToken cancellationToken = default)
    {
        foreach (RecurringJobRegistration reg in registrations)
        {
            _jobs.AddOrUpdate(
                reg.JobName,
                addValueFactory: _ => BackgroundJobDefinition.Create(
                    guidGenerator.Create(), reg.JobName, reg.CronExpression, reg.MessageType),
                updateValueFactory: (_, existing) =>
                {
                    // Preserve administrative state — only sync CronExpression changes.
                    existing.UpdateDefinition(reg.CronExpression, reg.MessageType);
                    return existing;
                });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordExecutionStartAsync(string jobName, DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job))
        {
            job.RecordExecutionStart(startedAt);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordNextExecutionAsync(string jobName, DateTimeOffset nextExecution, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job))
        {
            job.ScheduleNext(nextExecution);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordExecutionFailureAsync(string jobName, string errorMessage, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job))
        {
            job.RecordFailure(errorMessage);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetEnabledAsync(string jobName, bool enabled, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job))
        {
            if (enabled)
            {
                job.Resume();
            }
            else
            {
                job.Pause();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetTriggeredByAsync(string jobName, string? triggeredBy, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job))
        {
            job.SetTriggeredBy(triggeredBy);
        }

        return Task.CompletedTask;
    }
}
