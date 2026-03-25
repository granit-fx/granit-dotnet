using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Guids;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Maps <see cref="AuditingBatch"/> channel messages to EF Core entities.
/// </summary>
internal static class AuditingBatchMapper
{
    /// <summary>
    /// Converts a captured <see cref="AuditingBatch"/> to a persistable <see cref="AuditEntry"/>
    /// with all nested entity and property changes.
    /// </summary>
    public static AuditEntry ToEntity(AuditingBatch batch, IGuidGenerator guidGenerator)
    {
        AuditEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = batch.Timestamp,
            UserId = batch.UserId,
            UserName = batch.UserName,
            Category = batch.Category,
            IpAddress = batch.IpAddress,
            UserAgent = batch.UserAgent,
            TenantId = batch.TenantId,
            CorrelationId = batch.CorrelationId,
            CreatedAt = batch.Timestamp,
            CreatedBy = batch.UserId,
        };

        foreach (AuditEntityChangeSnapshot entitySnapshot in batch.EntityChanges)
        {
            AuditEntityChange entityChange = new()
            {
                Id = guidGenerator.Create(),
                AuditEntryId = entry.Id,
                EntityType = entitySnapshot.EntityType,
                EntityId = entitySnapshot.EntityId,
                ChangeType = entitySnapshot.ChangeType,
            };

            foreach (AuditPropertyChangeSnapshot propSnapshot in entitySnapshot.PropertyChanges)
            {
                entityChange.PropertyChanges.Add(new AuditPropertyChange
                {
                    Id = guidGenerator.Create(),
                    AuditEntityChangeId = entityChange.Id,
                    PropertyName = propSnapshot.PropertyName,
                    OriginalValue = propSnapshot.OriginalValue,
                    NewValue = propSnapshot.NewValue,
                });
            }

            entry.EntityChanges.Add(entityChange);
        }

        return entry;
    }
}
