using System.Diagnostics;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Events;
using Granit.Auditing.Messages;
using Granit.Events;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Persists <see cref="AuditingBatch"/> messages synchronously to the
/// <see cref="AuditingDbContext"/> for strict ISO 27001 compliance.
/// </summary>
internal sealed class StrictAuditingPublisher(
    IServiceScopeFactory scopeFactory,
    AuditingMetrics metrics) : IAuditEntryPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(AuditingBatch batch, CancellationToken cancellationToken = default)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDbContextFactory<AuditingDbContext> dbContextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditingDbContext>>();
        IGuidGenerator guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);
        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        metrics.RecordPersistenceDuration(elapsedMs, batch.TenantId?.ToString());
        metrics.RecordBatchSize(batch.EntityChanges.Count, batch.TenantId?.ToString());

        IDistributedEventBus eventBus = scope.ServiceProvider.GetRequiredService<IDistributedEventBus>();
        await eventBus.PublishAsync(new AuditEntryPersistedEto(
            entry.Id,
            batch.Timestamp,
            batch.UserId,
            batch.Category,
            batch.EntityChanges.Count,
            batch.TenantId), cancellationToken).ConfigureAwait(false);
    }
}
