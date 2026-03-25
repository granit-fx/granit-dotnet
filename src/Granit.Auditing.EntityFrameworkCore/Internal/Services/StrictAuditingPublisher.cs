using Granit.Auditing.Abstractions;
using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Persists <see cref="AuditingBatch"/> messages synchronously to the
/// <see cref="AuditingDbContext"/> for strict ISO 27001 compliance.
/// </summary>
internal sealed class StrictAuditingPublisher(
    IServiceScopeFactory scopeFactory) : IAuditEntryPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(AuditingBatch batch, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IDbContextFactory<AuditingDbContext> dbContextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditingDbContext>>();
        IGuidGenerator guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);
        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
