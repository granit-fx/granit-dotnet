using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Guids;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditBatchPersister"/>. Maps an
/// <see cref="AuditingBatch"/> to a persistable <see cref="AuditEntry"/> and saves it
/// through <see cref="AuditingContextResolver"/> — entries with a tenant scope route to
/// the tenant DB under Segregated; host-admin entries land in the host DB.
/// </summary>
internal sealed class EfCoreAuditBatchPersister(
    AuditingContextResolver resolver,
    IGuidGenerator guidGenerator) : IAuditBatchPersister
{
    /// <inheritdoc/>
    public async Task PersistAsync(AuditingBatch batch, CancellationToken cancellationToken = default)
    {
        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);

        await using IAuditingDbContext dbContext = await resolver
            .OpenForScopeAsync(entry.TenantId, cancellationToken).ConfigureAwait(false);

        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
