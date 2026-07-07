using System.Collections.Concurrent;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Options;
using Granit.Guids;
using Microsoft.Extensions.Options;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IBackgroundJobStoreReader"/> and
/// <see cref="IBackgroundJobStoreWriter"/>.
/// Registered as a <b>Singleton</b> default; replaced by the EF Core store when
/// <c>Granit.BackgroundJobs.EntityFrameworkCore</c> is added. State is lost on restart.
/// </summary>
/// <remarks>
/// Domain and integration events raised by <see cref="BackgroundJobDefinition"/> are
/// <b>not dispatched</b> by this store — there is no SaveChanges pipeline to publish them.
/// They are drained after each mutation to prevent unbounded accumulation on the
/// long-lived aggregates. Durable mode dispatches them via the EF Core interceptors.
/// </remarks>
internal sealed class InMemoryBackgroundJobStore(
    IGuidGenerator guidGenerator,
    IOptions<BackgroundJobsOptions> options) : IBackgroundJobStoreReader, IBackgroundJobStoreWriter
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
            BackgroundJobDefinition job = _jobs.AddOrUpdate(
                reg.JobName,
                addValueFactory: _ => BackgroundJobDefinition.Create(
                    guidGenerator.Create(), reg.JobName, reg.CronExpression, reg.MessageType),
                updateValueFactory: (_, existing) =>
                {
                    // Preserve administrative state — only sync CronExpression changes.
                    existing.UpdateDefinition(reg.CronExpression, reg.MessageType);
                    return existing;
                });
            DrainEvents(job);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RecordExecutionStartAsync(string jobName, DateTimeOffset startedAt, CancellationToken cancellationToken = default) =>
        MutateAsync(jobName, job => job.RecordExecutionStart(startedAt));

    /// <inheritdoc/>
    public Task RecordNextExecutionAsync(string jobName, DateTimeOffset nextExecution, CancellationToken cancellationToken = default) =>
        MutateAsync(jobName, job => job.ScheduleNext(nextExecution));

    /// <inheritdoc/>
    public Task RecordExecutionFailureAsync(string jobName, string errorMessage, CancellationToken cancellationToken = default) =>
        MutateAsync(jobName, job => job.RecordFailure(errorMessage, options.Value.FailureAlertThreshold));

    /// <inheritdoc/>
    public Task SetEnabledAsync(string jobName, bool enabled, CancellationToken cancellationToken = default) =>
        MutateAsync(jobName, job =>
        {
            if (enabled)
            {
                job.Resume();
            }
            else
            {
                job.Pause();
            }
        });

    /// <inheritdoc/>
    public Task SetTriggeredByAsync(string jobName, string? triggeredBy, CancellationToken cancellationToken = default) =>
        MutateAsync(jobName, job => job.SetTriggeredBy(triggeredBy));

    private Task MutateAsync(string jobName, Action<BackgroundJobDefinition> mutation)
    {
        if (_jobs.TryGetValue(jobName, out BackgroundJobDefinition? job))
        {
            mutation(job);
            DrainEvents(job);
        }

        return Task.CompletedTask;
    }

    private static void DrainEvents(BackgroundJobDefinition job)
    {
        job.ClearDomainEvents();
        job.ClearIntegrationEvents();
    }
}
