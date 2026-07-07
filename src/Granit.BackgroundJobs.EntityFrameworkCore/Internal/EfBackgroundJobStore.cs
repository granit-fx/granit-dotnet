using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Options;
using Granit.Guids;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Internal;

/// <summary>
/// Durable EF Core implementation of <see cref="IBackgroundJobStoreReader"/> and
/// <see cref="IBackgroundJobStoreWriter"/> — replaces the in-memory default when
/// <c>AddGranitBackgroundJobsEntityFrameworkCore()</c> is called.
/// </summary>
/// <remarks>
/// Registered as <b>Scoped</b>: <c>AddGranitDbContext</c> registers
/// <see cref="IDbContextFactory{TContext}"/> as Scoped (required so interceptors can resolve
/// <c>ICurrentTenant</c> and <c>ICurrentUser</c>); this store must therefore also be Scoped
/// to avoid captive dependency violations.
/// Each operation creates and disposes its own <see cref="BackgroundJobsDbContext"/> via the factory.
/// </remarks>
internal sealed partial class EfBackgroundJobStore(
    IDbContextFactory<BackgroundJobsDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IOptions<BackgroundJobsOptions> options,
    ILogger<EfBackgroundJobStore> logger)
    : EfStoreBase<BackgroundJobDefinition, BackgroundJobsDbContext>(contextFactory),
      IBackgroundJobStoreReader, IBackgroundJobStoreWriter
{
    /// <inheritdoc/>
    public Task<BackgroundJobDefinition?> FindAsync(
        string jobName,
        CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<BackgroundJobDefinition>> GetEnabledJobsAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BackgroundJobDefinition>().Where(j => j.IsEnabled),
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<BackgroundJobDefinition>> GetAllJobsAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<BackgroundJobDefinition>(),
            cancellationToken);

    /// <inheritdoc/>
    public Task SeedJobsAsync(
        IEnumerable<RecurringJobRegistration> registrations,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                foreach (RecurringJobRegistration reg in registrations)
                {
                    BackgroundJobDefinition? existing =
                        await db.Jobs.FirstOrDefaultAsync(j => j.JobName == reg.JobName, cancellationToken)
                            .ConfigureAwait(false);

                    if (existing is null)
                    {
                        db.Jobs.Add(BackgroundJobDefinition.Create(
                            guidGenerator.Create(), reg.JobName, reg.CronExpression, reg.MessageType));
                    }
                    else
                    {
                        // Preserve administrative state — only sync scheduling metadata.
                        existing.UpdateDefinition(reg.CronExpression, reg.MessageType);
                    }
                }
            },
            cancellationToken);

    /// <inheritdoc/>
    public Task RecordExecutionStartAsync(
        string jobName,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken = default) =>
        MutateJobAsync(jobName, job => job.RecordExecutionStart(startedAt), cancellationToken);

    /// <inheritdoc/>
    public Task RecordNextExecutionAsync(
        string jobName,
        DateTimeOffset nextExecution,
        CancellationToken cancellationToken = default) =>
        MutateJobAsync(jobName, job => job.ScheduleNext(nextExecution), cancellationToken);

    /// <inheritdoc/>
    public Task RecordExecutionFailureAsync(
        string jobName,
        string errorMessage,
        CancellationToken cancellationToken = default) =>
        MutateJobAsync(
            jobName,
            job => job.RecordFailure(errorMessage, options.Value.FailureAlertThreshold),
            cancellationToken);

    /// <inheritdoc/>
    public Task SetEnabledAsync(
        string jobName,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        MutateJobAsync(
            jobName,
            job =>
            {
                if (enabled)
                {
                    job.Resume();
                }
                else
                {
                    job.Pause();
                }
            },
            cancellationToken);

    /// <inheritdoc/>
    public Task SetTriggeredByAsync(
        string jobName,
        string? triggeredBy,
        CancellationToken cancellationToken = default) =>
        MutateJobAsync(jobName, job => job.SetTriggeredBy(triggeredBy), cancellationToken);

    /// <summary>
    /// Fetches a job by name and applies a mutation. No-ops (with a Debug trace) if the
    /// job does not exist.
    /// </summary>
    private Task MutateJobAsync(
        string jobName,
        Action<BackgroundJobDefinition> mutation,
        CancellationToken cancellationToken) =>
        WriteAsync(
            async db =>
            {
                BackgroundJobDefinition? job = await db.Jobs
                    .FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken)
                    .ConfigureAwait(false);

                if (job is null)
                {
                    LogJobNotFoundForMutation(jobName);
                    return;
                }

                mutation(job);
            },
            cancellationToken);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "BackgroundJob '{JobName}' not found in store — mutation skipped")]
    private partial void LogJobNotFoundForMutation(string jobName);
}
