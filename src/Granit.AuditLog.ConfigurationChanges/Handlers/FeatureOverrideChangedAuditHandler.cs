using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.Core.Events;
using Granit.Core.MultiTenancy;
using Granit.Features.Events;
using Granit.Guids;
using Granit.Security;

namespace Granit.AuditLog.ConfigurationChanges.Handlers;

/// <summary>
/// Persists <see cref="FeatureOverrideChangedEvent"/> as an <see cref="AuditLogEntry"/>
/// with category <see cref="AuditLogCategory.ConfigurationChange"/>.
/// </summary>
public sealed class FeatureOverrideChangedAuditHandler(
    IAuditLogWriter writer,
    ICurrentUserService currentUser,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator) : ILocalEventHandler<FeatureOverrideChangedEvent>
{
    /// <inheritdoc/>
    public async Task HandleAsync(FeatureOverrideChangedEvent localEvent, CancellationToken cancellationToken = default)
    {
        AuditPropertyChange valueChange = new()
        {
            Id = guidGenerator.Create(),
            PropertyName = "Value",
            OriginalValue = localEvent.OldValue,
            NewValue = localEvent.NewValue,
        };

        AuditEntityChange entityChange = new()
        {
            Id = guidGenerator.Create(),
            EntityType = "FeatureOverride",
            EntityId = localEvent.FeatureName,
            ChangeType = DetermineChangeType(localEvent),
            PropertyChanges = [valueChange],
        };

        AuditLogEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = localEvent.Timestamp,
            UserId = currentUser.UserId ?? "system",
            UserName = currentUser.UserName,
            Category = AuditLogCategory.ConfigurationChange,
            TenantId = localEvent.TenantId
                       ?? (currentTenant.IsAvailable ? currentTenant.Id : null),
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            EntityChanges = [entityChange],
        };

        valueChange.AuditEntityChangeId = entityChange.Id;
        entityChange.AuditLogEntryId = entry.Id;

        await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static AuditChangeType DetermineChangeType(FeatureOverrideChangedEvent e) => e switch
    {
        { OldValue: null, NewValue: not null } => AuditChangeType.Created,
        { OldValue: not null, NewValue: null } => AuditChangeType.Deleted,
        _ => AuditChangeType.Modified,
    };
}
