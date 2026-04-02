using System.Diagnostics;
using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Events;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.Events;
using Granit.Guids;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.Internal.Services;

/// <summary>
/// Background service that reads <see cref="AuditingBatch"/> messages from the channel
/// and persists them via <see cref="IAuditBatchPersister"/>.
/// </summary>
/// <remarks>
/// Only active in <see cref="AuditPersistenceMode.Async"/> mode.
/// </remarks>
internal sealed partial class AuditingPersistenceWorker(
    Channel<AuditingBatch> channel,
    IServiceScopeFactory scopeFactory,
    IOptions<AuditingOptions> options,
    AuditingMetrics metrics,
    ILogger<AuditingPersistenceWorker> logger) : BackgroundService
{
    private const int MaxRetryAttempts = 3;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
    ];

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PersistenceMode == AuditPersistenceMode.Async)
        {
            LogAsyncModeWarning();
        }

        await foreach (AuditingBatch batch in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await PersistWithRetryAsync(batch, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PersistWithRetryAsync(AuditingBatch batch, CancellationToken stoppingToken)
    {
        using Activity? activity = AuditingActivitySource.Source.StartActivity(AuditingActivitySource.Persist);
        activity?.SetTag("tenant_id", batch.TenantId?.ToString() ?? "global");
        activity?.SetTag("audit.category", batch.Category.ToString());
        activity?.SetTag("audit.entity_change_count", batch.EntityChanges.Count);

        long startTimestamp = Stopwatch.GetTimestamp();

        for (int attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IAuditBatchPersister persister = scope.ServiceProvider.GetRequiredService<IAuditBatchPersister>();

                await persister.PersistAsync(batch, stoppingToken).ConfigureAwait(false);

                double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                metrics.RecordPersistenceDuration(elapsedMs, batch.TenantId?.ToString());
                metrics.RecordBatchSize(batch.EntityChanges.Count, batch.TenantId?.ToString());
                metrics.RecordPersisted(1, batch.TenantId?.ToString());
                LogEntryPersisted(batch.EntityChanges.Count);

                IDistributedEventBus eventBus = scope.ServiceProvider.GetRequiredService<IDistributedEventBus>();
                IGuidGenerator guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
                await eventBus.PublishAsync(new AuditEntryPersistedEto(
                    guidGenerator.Create(),
                    batch.Timestamp,
                    batch.UserId,
                    batch.Category,
                    batch.EntityChanges.Count,
                    batch.TenantId), stoppingToken).ConfigureAwait(false);

                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts)
            {
                metrics.RecordPersistenceRetry(batch.TenantId?.ToString());
                LogPersistenceRetry(attempt + 1, MaxRetryAttempts, ex);
                await Task.Delay(RetryDelays[attempt], stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogPersistenceDropped(ex);
                metrics.RecordCaptureError(batch.TenantId?.ToString());
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Audit log entry persisted with {EntityChangeCount} entity changes")]
    private partial void LogEntryPersisted(int entityChangeCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Audit persistence attempt {Attempt}/{MaxAttempts} failed, retrying")]
    private partial void LogPersistenceRetry(int attempt, int maxAttempts, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "Audit log entry dropped after all retry attempts — audit trail gap (ISO 27001 A.12.4)")]
    private partial void LogPersistenceDropped(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Audit persistence mode is Async — audit entries are buffered in-memory and will be lost on process crash. " +
                  "Set PersistenceMode to Strict for ISO 27001 compliance in production environments")]
    private partial void LogAsyncModeWarning();
}
