using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IBackgroundJobStoreReader"/> and
/// <see cref="IBackgroundJobStoreWriter"/>.
/// </summary>
/// <remarks>
/// Registered as <b>Scoped</b> when <see cref="JobStoreMode.Durable"/> is configured.
/// <c>AddGranitDbContext</c> registers <see cref="IDbContextFactory{TContext}"/> as Scoped
/// (required so interceptors can resolve <c>ICurrentTenant</c> and <c>ICurrentUser</c>);
/// this store must therefore also be Scoped to avoid captive dependency violations.
/// Each operation creates and disposes its own <see cref="BackgroundJobsDbContext"/> via the factory.
/// </remarks>
internal sealed class EfBackgroundJobStore(
    IDbContextFactory<BackgroundJobsDbContext> contextFactory,
    IGuidGenerator guidGenerator) : IBackgroundJobStoreReader, IBackgroundJobStoreWriter
{
    /// <inheritdoc/>
    public async Task<BackgroundJobDefinition?> FindAsync(string jobName, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Jobs.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BackgroundJobDefinition>> GetEnabledJobsAsync(CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Jobs.Where(j => j.IsEnabled).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BackgroundJobDefinition>> GetAllJobsAsync(CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Jobs.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SeedJobsAsync(IEnumerable<RecurringJobRegistration> registrations, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        foreach (RecurringJobRegistration reg in registrations)
        {
            BackgroundJobDefinition? existing =
                await context.Jobs.FirstOrDefaultAsync(j => j.JobName == reg.JobName, cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                context.Jobs.Add(new BackgroundJobDefinition
                {
                    Id = guidGenerator.Create(),
                    JobName = reg.JobName,
                    CronExpression = reg.CronExpression,
                    MessageType = reg.MessageType,
                    IsEnabled = true,
                });
            }
            else
            {
                // Preserve administrative state — only sync scheduling metadata.
                existing.CronExpression = reg.CronExpression;
                existing.MessageType = reg.MessageType;
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecordExecutionStartAsync(string jobName, DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        BackgroundJobDefinition? job = await context.Jobs.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return;
        }

        job.LastExecutedAt = startedAt;
        job.LastErrorMessage = null;
        job.ConsecutiveFailureCount = 0;
        job.TriggeredBy = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecordNextExecutionAsync(string jobName, DateTimeOffset nextExecution, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        BackgroundJobDefinition? job = await context.Jobs.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return;
        }

        job.NextExecutionAt = nextExecution;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecordExecutionFailureAsync(string jobName, string errorMessage, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        BackgroundJobDefinition? job = await context.Jobs.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return;
        }

        job.ConsecutiveFailureCount++;
        job.LastErrorMessage = errorMessage;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetEnabledAsync(string jobName, bool enabled, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        BackgroundJobDefinition? job = await context.Jobs.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return;
        }

        if (enabled)
        {
            job.Resume();
        }
        else
        {
            job.Pause();
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetTriggeredByAsync(string jobName, string? triggeredBy, CancellationToken cancellationToken = default)
    {
        await using BackgroundJobsDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        BackgroundJobDefinition? job = await context.Jobs.FirstOrDefaultAsync(j => j.JobName == jobName, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return;
        }

        job.TriggeredBy = triggeredBy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
