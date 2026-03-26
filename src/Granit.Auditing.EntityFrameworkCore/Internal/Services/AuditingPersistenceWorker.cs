using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Background service that reads <see cref="AuditingBatch"/> messages from the channel
/// and persists them to the <see cref="AuditingDbContext"/>.
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
        for (int attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IDbContextFactory<AuditingDbContext> dbContextFactory =
                    scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditingDbContext>>();
                IGuidGenerator guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

                await using AuditingDbContext dbContext = await dbContextFactory
                    .CreateDbContextAsync(stoppingToken).ConfigureAwait(false);

                AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);
                dbContext.AuditEntries.Add(entry);
                await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

                metrics.RecordPersisted(1, batch.TenantId?.ToString());
                LogEntryPersisted(entry.Id, batch.EntityChanges.Count);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts)
            {
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
        Message = "Audit log entry {EntryId} persisted with {EntityChangeCount} entity changes")]
    private partial void LogEntryPersisted(Guid entryId, int entityChangeCount);

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
