using System.Diagnostics;
using System.Reflection;
using Granit.BackgroundJobs.Diagnostics;
using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.BackgroundJobs.Wolverine;

/// <summary>
/// Wolverine middleware that records job execution and atomically reschedules the next
/// occurrence inside the Outbox transaction after each successful handler invocation.
/// </summary>
/// <remarks>
/// <para>
/// Injected automatically via <c>opts.Policies.AddMiddleware</c> on all handler chains
/// whose message type carries <see cref="RecurringJobAttribute"/>. Never applied manually.
/// </para>
/// <para>
/// <b>Anti-doublon guarantee:</b> <see cref="AfterAsync"/> calls
/// <c>IMessageContext.ScheduleAsync</c> inside the same database transaction as
/// the handler. If the node crashes before the transaction commits, Wolverine redelivers
/// the current message — the "next" message was never inserted, so no duplicate is created.
/// Manual triggers (<see cref="BackgroundJobHeaders.ManualTrigger"/>) never advance the
/// chain, and an occurrence already armed by the failure path is never armed twice.
/// </para>
/// <para>
/// <b>Failure accounting:</b> <see cref="OnExceptionAsync"/> records the failure
/// (consecutive counter, truncated error message, failure metrics) and re-arms the next
/// occurrence via <see cref="IMessageBus"/> — outside the enveloppe transaction, which is
/// about to roll back — so a failing handler never silently stops the recurrence.
/// </para>
/// <para>
/// <b>ISO 27001 audit:</b> <see cref="BeforeAsync"/> reads the <c>X-Triggered-By</c> header
/// (set by <see cref="IBackgroundJobWriter.TriggerNowAsync"/>) and persists it via
/// <see cref="IBackgroundJobStoreWriter.SetTriggeredByAsync"/>.
/// </para>
/// </remarks>
public sealed partial class RecurringJobSchedulingMiddleware(
    IBackgroundJobStoreReader storeReader,
    IBackgroundJobStoreWriter storeWriter,
    IClock clock,
    BackgroundJobsMetrics metrics,
    ILogger<RecurringJobSchedulingMiddleware> logger)
{
    private long _startTimestamp;

    /// <summary>
    /// Records the execution start and captures the <c>X-Triggered-By</c> header for ISO 27001 audit.
    /// </summary>
    public async Task BeforeAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        RecurringJobAttribute? attr = envelope.Message?.GetType()
            .GetCustomAttribute<RecurringJobAttribute>();

        if (attr is null)
        {
            return;
        }

        _startTimestamp = Stopwatch.GetTimestamp();

        await storeWriter.RecordExecutionStartAsync(attr.Name, clock.Now, cancellationToken).ConfigureAwait(false);

        if (envelope.Headers.TryGetValue(BackgroundJobHeaders.TriggeredBy, out string? triggeredBy)
            && !string.IsNullOrEmpty(triggeredBy))
        {
            await storeWriter.SetTriggeredByAsync(attr.Name, triggeredBy, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Calculates the next cron occurrence and schedules the message inside the Outbox
    /// transaction. Skipped when the job is paused, when the execution was manually
    /// triggered, or when the occurrence is already armed.
    /// </summary>
    public async Task AfterAsync(Envelope envelope, IMessageContext context, CancellationToken cancellationToken)
    {
        RecurringJobAttribute? attr = envelope.Message?.GetType()
            .GetCustomAttribute<RecurringJobAttribute>();

        if (attr is null)
        {
            return;
        }

        RecordMetrics(attr.Name, "success");

        (object Message, DateTimeOffset Next)? occurrence =
            await PrepareNextOccurrenceAsync(envelope, attr, cancellationToken).ConfigureAwait(false);
        if (occurrence is not { } next)
        {
            return;
        }

        await context.ScheduleAsync(next.Message, next.Next).ConfigureAwait(false);
        await storeWriter.RecordNextExecutionAsync(attr.Name, next.Next, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records the failure (consecutive counter, truncated error, failure metrics) and
    /// re-arms the next occurrence so a failing handler never stops the recurring chain.
    /// The next message is scheduled via <paramref name="bus"/> — not the envelope
    /// context — because the envelope transaction is rolling back.
    /// </summary>
    public async Task OnExceptionAsync(
        Exception exception,
        Envelope envelope,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        RecurringJobAttribute? attr = envelope.Message?.GetType()
            .GetCustomAttribute<RecurringJobAttribute>();

        if (attr is null)
        {
            return;
        }

        RecordMetrics(attr.Name, "failure");
        LogJobExecutionFailed(logger, attr.Name, exception);

        await storeWriter.RecordExecutionFailureAsync(attr.Name, exception.Message, cancellationToken)
            .ConfigureAwait(false);

        (object Message, DateTimeOffset Next)? occurrence =
            await PrepareNextOccurrenceAsync(envelope, attr, cancellationToken).ConfigureAwait(false);
        if (occurrence is not { } next)
        {
            return;
        }

        await bus.ScheduleAsync(next.Message, next.Next).ConfigureAwait(false);
        await storeWriter.RecordNextExecutionAsync(attr.Name, next.Next, cancellationToken).ConfigureAwait(false);
    }

    private void RecordMetrics(string jobName, string status)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        metrics.RecordExecutionCompleted(null, jobName, status);
        metrics.RecordExecutionDuration(null, jobName, status, elapsed);
    }

    /// <summary>
    /// Computes the next occurrence to arm, or <c>null</c> when nothing must be scheduled:
    /// manual trigger, paused/unknown job, no next occurrence, or occurrence already armed
    /// (e.g. the failure path armed it before a retry succeeded).
    /// </summary>
    private async Task<(object Message, DateTimeOffset Next)?> PrepareNextOccurrenceAsync(
        Envelope envelope,
        RecurringJobAttribute attr,
        CancellationToken cancellationToken)
    {
        // A manual trigger must not advance the recurring chain: the regular occurrence
        // stays armed, so rescheduling here would permanently double the recurrence.
        if (envelope.Headers.ContainsKey(BackgroundJobHeaders.ManualTrigger))
        {
            return null;
        }

        BackgroundJobDefinition? job = await storeReader.FindAsync(attr.Name, cancellationToken).ConfigureAwait(false);
        if (job is not { IsEnabled: true })
        {
            return null;
        }

        // Base the computation on the occurrence being executed when the local clock lags
        // behind it (cluster clock skew) — otherwise the current occurrence would be
        // recomputed as "next" and the guard below would kill the chain.
        DateTimeOffset from = envelope.ScheduledTime > clock.Now ? envelope.ScheduledTime.Value : clock.Now;
        DateTimeOffset? next = CronSchedulerHelper.ComputeNext(job.CronExpression, from);
        if (next is null)
        {
            LogNoCronOccurrence(logger, job.JobName, job.CronExpression);
            return null;
        }

        if (job.NextExecutionAt == next)
        {
            return null;
        }

        return (Activator.CreateInstance(envelope.Message!.GetType())!, next.Value);
    }

    // =========================================================================
    // Source-generated logger messages (CA1873 / CA1848 compliance)
    // =========================================================================

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RecurringJob '{JobName}': cron expression '{Cron}' produced no next occurrence.")]
    private static partial void LogNoCronOccurrence(ILogger logger, string jobName, string cron);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "RecurringJob '{JobName}': handler execution failed.")]
    private static partial void LogJobExecutionFailed(ILogger logger, string jobName, Exception exception);
}
