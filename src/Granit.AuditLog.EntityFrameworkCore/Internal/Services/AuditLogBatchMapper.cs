using Granit.AuditLog.Domain;
using Granit.AuditLog.Messages;
using Granit.Guids;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Maps <see cref="AuditLogBatch"/> channel messages to EF Core entities.
/// </summary>
internal static class AuditLogBatchMapper
{
    /// <summary>
    /// Converts a captured <see cref="AuditLogBatch"/> to a persistable <see cref="AuditLogEntry"/>
    /// with all nested entity and property changes.
    /// </summary>
    public static AuditLogEntry ToEntity(AuditLogBatch batch, IGuidGenerator guidGenerator)
    {
        AuditLogEntry entry = new()
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
                AuditLogEntryId = entry.Id,
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
