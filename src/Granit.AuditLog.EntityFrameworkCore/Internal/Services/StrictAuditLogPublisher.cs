using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.Messages;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Persists <see cref="AuditLogBatch"/> messages synchronously to the
/// <see cref="AuditLogDbContext"/> for strict ISO 27001 compliance.
/// </summary>
internal sealed class StrictAuditLogPublisher(
    IServiceScopeFactory scopeFactory) : IAuditLogEntryPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(AuditLogBatch batch, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDbContextFactory<AuditLogDbContext> dbContextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditLogDbContext>>();
        IGuidGenerator guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        await using AuditLogDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AuditLogEntry entry = AuditLogBatchMapper.ToEntity(batch, guidGenerator);
        dbContext.AuditLogEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
