using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.Core.MultiTenancy;
using Granit.Features.Events;
using Granit.Guids;
using Granit.Security;

namespace Granit.AuditLog.Wolverine.Handlers;

/// <summary>
/// Wolverine handler that persists <see cref="FeatureOverrideChangedEvent"/> as an
/// <see cref="AuditLogEntry"/> with category <see cref="AuditLogCategory.ConfigurationChange"/>.
/// </summary>
public static class FeatureOverrideChangedAuditHandler
{
    /// <summary>
    /// Converts a feature override change event into an audit log entry and persists it.
    /// </summary>
    public static async Task HandleAsync(
        FeatureOverrideChangedEvent featureOverrideChangedEvent,
        IAuditLogWriter writer,
        ICurrentUserService currentUser,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        CancellationToken cancellationToken)
    {
        AuditPropertyChange valueChange = new()
        {
            Id = guidGenerator.Create(),
            PropertyName = "Value",
            OriginalValue = featureOverrideChangedEvent.OldValue,
            NewValue = featureOverrideChangedEvent.NewValue,
        };

        AuditEntityChange entityChange = new()
        {
            Id = guidGenerator.Create(),
            EntityType = "FeatureOverride",
            EntityId = featureOverrideChangedEvent.FeatureName,
            ChangeType = DetermineChangeType(featureOverrideChangedEvent),
            PropertyChanges = [valueChange],
        };

        AuditLogEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = featureOverrideChangedEvent.Timestamp,
            UserId = currentUser.UserId ?? "system",
            UserName = currentUser.UserName,
            Category = AuditLogCategory.ConfigurationChange,
            TenantId = featureOverrideChangedEvent.TenantId
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
