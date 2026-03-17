using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Security;
using Granit.Settings.Events;

namespace Granit.AuditLog.Wolverine.Handlers;

/// <summary>
/// Wolverine handler that persists <see cref="SettingChangedEvent"/> as an
/// <see cref="AuditLogEntry"/> with category <see cref="AuditLogCategory.ConfigurationChange"/>.
/// </summary>
public static class SettingChangedAuditHandler
{
    /// <summary>
    /// Converts a setting change event into an audit log entry and persists it.
    /// </summary>
    public static async Task HandleAsync(
        SettingChangedEvent settingChangedEvent,
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
            OriginalValue = settingChangedEvent.OldValue,
            NewValue = settingChangedEvent.NewValue,
        };

        AuditEntityChange entityChange = new()
        {
            Id = guidGenerator.Create(),
            EntityType = "Setting",
            EntityId = $"{settingChangedEvent.SettingName}:{settingChangedEvent.ProviderName}:{settingChangedEvent.ProviderKey ?? "global"}",
            ChangeType = DetermineChangeType(settingChangedEvent),
            PropertyChanges = [valueChange],
        };

        AuditLogEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = settingChangedEvent.Timestamp,
            UserId = currentUser.UserId ?? "system",
            UserName = currentUser.UserName,
            Category = AuditLogCategory.ConfigurationChange,
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            EntityChanges = [entityChange],
        };

        valueChange.AuditEntityChangeId = entityChange.Id;
        entityChange.AuditLogEntryId = entry.Id;

        await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static AuditChangeType DetermineChangeType(SettingChangedEvent e) => e switch
    {
        { OldValue: null, NewValue: not null } => AuditChangeType.Created,
        { OldValue: not null, NewValue: null } => AuditChangeType.Deleted,
        _ => AuditChangeType.Modified,
    };
}
