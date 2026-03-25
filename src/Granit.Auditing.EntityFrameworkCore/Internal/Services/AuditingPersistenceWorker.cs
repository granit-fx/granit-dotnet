using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    AuditingMetrics metrics,
    ILogger<AuditingPersistenceWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (AuditingBatch batch in channel.Reader.ReadAllAsync(stoppingToken))
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPersistenceFailed(ex);
                metrics.RecordCaptureError(batch.TenantId?.ToString());
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Audit log entry {EntryId} persisted with {EntityChangeCount} entity changes")]
    private partial void LogEntryPersisted(Guid entryId, int entityChangeCount);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to persist audit log entry")]
    private partial void LogPersistenceFailed(Exception exception);
}
