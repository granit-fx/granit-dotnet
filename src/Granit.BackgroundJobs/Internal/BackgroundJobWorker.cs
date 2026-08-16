using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Diagnostics;
using Granit.BackgroundJobs.Domain;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Background service that reads <see cref="BackgroundJobEnvelope"/> from the channel
/// and invokes job handlers in a DI scope. Replicates the scheduling middleware logic
/// for the in-process dispatch path.
/// </summary>
/// <remarks>
/// Handler resolution is delegated to <see cref="BackgroundJobHandlerResolver"/>, which
/// mirrors Wolverine's binding rule (the handler method's first parameter type, not the
/// handler class name) so both dispatch paths invoke the same handler. Remaining method
/// parameters are resolved from the DI scope; a <see cref="CancellationToken"/> parameter
/// is honoured. Static handler classes are supported — instance handlers are resolved from
/// DI or activated via <see cref="ActivatorUtilities"/>.
/// </remarks>
internal sealed partial class BackgroundJobWorker(
    Channel<BackgroundJobEnvelope> channel,
    IServiceScopeFactory scopeFactory,
    IClock clock,
    BackgroundJobsMetrics metrics,
    ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (BackgroundJobEnvelope envelope in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(envelope, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogExecutionFailed(envelope.Message.GetType().Name, ex);
            }
        }
    }

    private async Task ProcessAsync(BackgroundJobEnvelope envelope, CancellationToken cancellationToken)
    {
        RecurringJobAttribute? attr = envelope.Message.GetType()
            .GetCustomAttribute<RecurringJobAttribute>();
        bool isManualTrigger = envelope.Headers?.ContainsKey(BackgroundJobHeaders.ManualTrigger) == true;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        if (attr is not null)
        {
            IBackgroundJobStoreWriter storeWriter =
                scope.ServiceProvider.GetRequiredService<IBackgroundJobStoreWriter>();

            // Record execution start + triggered-by header (ISO 27001 audit).
            await storeWriter.RecordExecutionStartAsync(attr.Name, clock.Now, cancellationToken)
                .ConfigureAwait(false);

            if (envelope.Headers?.TryGetValue(BackgroundJobHeaders.TriggeredBy, out string? triggeredBy) == true
                && !string.IsNullOrEmpty(triggeredBy))
            {
                await storeWriter.SetTriggeredByAsync(attr.Name, triggeredBy, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            // Invoke the handler Wolverine would bind for this message type.
            await InvokeHandlerAsync(scope.ServiceProvider, envelope.Message, cancellationToken)
                .ConfigureAwait(false);

            if (attr is not null)
            {
                RecordMetrics(attr.Name, "success", startTimestamp);

                // A manual trigger must not advance the recurring chain — the regular
                // occurrence stays armed (contract of IBackgroundJobWriter.TriggerNowAsync).
                if (!isManualTrigger)
                {
                    await RescheduleAsync(scope.ServiceProvider, attr, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (attr is not null)
            {
                RecordMetrics(attr.Name, "failure", startTimestamp);

                IBackgroundJobStoreWriter storeWriter =
                    scope.ServiceProvider.GetRequiredService<IBackgroundJobStoreWriter>();
                await storeWriter.RecordExecutionFailureAsync(attr.Name, ex.Message, cancellationToken)
                    .ConfigureAwait(false);

                // Keep the recurrence alive: a failed run must not stop the chain.
                if (!isManualTrigger)
                {
                    await RescheduleAsync(scope.ServiceProvider, attr, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            throw;
        }
    }

    private void RecordMetrics(string jobName, string status, long startTimestamp)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        metrics.RecordExecutionCompleted(null, jobName, status);
        metrics.RecordExecutionDuration(null, jobName, status, elapsed);
    }

    /// <summary>
    /// Resolves the handler for the message type via <see cref="BackgroundJobHandlerResolver"/>
    /// and invokes its handle method: the message as first argument, services from the scope
    /// for the remaining parameters, and the ambient <see cref="CancellationToken"/>.
    /// </summary>
    private static async Task InvokeHandlerAsync(
        IServiceProvider services,
        object message,
        CancellationToken cancellationToken)
    {
        (Type handlerType, MethodInfo method) = BackgroundJobHandlerResolver.Resolve(message.GetType());

        ParameterInfo[] parameters = method.GetParameters();
        object?[] args = new object?[parameters.Length];
        args[0] = message;
        for (int i = 1; i < parameters.Length; i++)
        {
            args[i] = parameters[i].ParameterType == typeof(CancellationToken)
                ? cancellationToken
                : services.GetRequiredService(parameters[i].ParameterType);
        }

        object? instance = method.IsStatic
            ? null
            : ActivatorUtilities.GetServiceOrCreateInstance(services, handlerType);

        object? result = method.Invoke(instance, args);
        switch (result)
        {
            case Task task:
                await task.ConfigureAwait(false);
                break;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                break;
        }
    }

    private async Task RescheduleAsync(
        IServiceProvider services,
        RecurringJobAttribute attr,
        CancellationToken cancellationToken)
    {
        IBackgroundJobStoreReader storeReader =
            services.GetRequiredService<IBackgroundJobStoreReader>();
        IBackgroundJobStoreWriter storeWriter =
            services.GetRequiredService<IBackgroundJobStoreWriter>();
        IBackgroundJobDispatcher dispatcher =
            services.GetRequiredService<IBackgroundJobDispatcher>();

        BackgroundJobDefinition? job = await storeReader.FindAsync(attr.Name, cancellationToken)
            .ConfigureAwait(false);

        if (job is not { IsEnabled: true })
        {
            return;
        }

        DateTimeOffset? next = CronSchedulerHelper.ComputeNext(job.CronExpression, clock.Now);
        if (next is null)
        {
            LogNoCronOccurrence(attr.Name, job.CronExpression);
            return;
        }

        // Already armed for that exact occurrence (e.g. failure path ran before a retry
        // succeeded) — do not schedule a duplicate.
        if (job.NextExecutionAt == next)
        {
            return;
        }

        object nextMessage = CronSchedulerHelper.CreateMessage(job.MessageType, job.JobName);
        await dispatcher.ScheduleAsync(nextMessage, next.Value, cancellationToken).ConfigureAwait(false);
        await storeWriter.RecordNextExecutionAsync(job.JobName, next.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Background job execution failed for message type '{MessageTypeName}'")]
    private partial void LogExecutionFailed(string messageTypeName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "RecurringJob '{JobName}': cron expression '{Cron}' produced no next occurrence")]
    private partial void LogNoCronOccurrence(string jobName, string cron);
}
