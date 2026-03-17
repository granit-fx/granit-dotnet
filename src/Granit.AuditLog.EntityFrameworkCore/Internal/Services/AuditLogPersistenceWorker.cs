using System.Threading.Channels;
using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Diagnostics;
using Granit.AuditLog.Messages;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Background service that reads <see cref="AuditLogBatch"/> messages from the channel
/// and persists them to the <see cref="AuditLogDbContext"/>.
/// </summary>
/// <remarks>
/// Only active in <see cref="AuditPersistenceMode.Async"/> mode.
/// </remarks>
internal sealed partial class AuditLogPersistenceWorker(
    Channel<AuditLogBatch> channel,
    IServiceScopeFactory scopeFactory,
    AuditLogMetrics metrics,
    ILogger<AuditLogPersistenceWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (AuditLogBatch batch in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IDbContextFactory<AuditLogDbContext> dbContextFactory =
                    scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditLogDbContext>>();
                IGuidGenerator guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

                await using AuditLogDbContext dbContext = await dbContextFactory
                    .CreateDbContextAsync(stoppingToken).ConfigureAwait(false);

                AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);
                dbContext.AuditLogEntries.Add(entry);
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
