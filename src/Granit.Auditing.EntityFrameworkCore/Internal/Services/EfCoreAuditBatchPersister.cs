using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditBatchPersister"/>.
/// Maps an <see cref="AuditingBatch"/> to a persistable <see cref="AuditEntry"/>
/// and saves it to the <see cref="AuditingDbContext"/>.
/// </summary>
internal sealed class EfCoreAuditBatchPersister(
    IDbContextFactory<AuditingDbContext> dbContextFactory,
    IGuidGenerator guidGenerator) : IAuditBatchPersister
{
    /// <inheritdoc/>
    public async Task PersistAsync(AuditingBatch batch, CancellationToken cancellationToken = default)
    {
        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);
        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
