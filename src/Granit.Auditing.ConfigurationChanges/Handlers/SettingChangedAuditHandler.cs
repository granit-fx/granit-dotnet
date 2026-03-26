using Granit.Auditing.Abstractions;
using Granit.Auditing.ConfigurationChanges.Internal;
using Granit.Auditing.Domain;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Settings.Events;
using Granit.Users;

namespace Granit.Auditing.ConfigurationChanges.Handlers;

/// <summary>
/// Persists <see cref="SettingChangedEvent"/> as an <see cref="AuditEntry"/>
/// with category <see cref="AuditCategory.ConfigurationChange"/>.
/// </summary>
public sealed class SettingChangedAuditHandler(
    IAuditingWriter writer,
    ICurrentUserService currentUser,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator) : ILocalEventHandler<SettingChangedEvent>
{
    /// <inheritdoc/>
    public async Task HandleAsync(SettingChangedEvent localEvent, CancellationToken cancellationToken = default)
    {
        AuditPropertyChange valueChange = new()
        {
            Id = guidGenerator.Create(),
            PropertyName = "Value",
            OriginalValue = SensitiveValueMasker.MaskIfSensitive(localEvent.SettingName, localEvent.OldValue),
            NewValue = SensitiveValueMasker.MaskIfSensitive(localEvent.SettingName, localEvent.NewValue),
        };

        AuditEntityChange entityChange = new()
        {
            Id = guidGenerator.Create(),
            EntityType = "Setting",
            EntityId = $"{localEvent.SettingName}:{localEvent.ProviderName}:{localEvent.ProviderKey ?? "global"}",
            ChangeType = DetermineChangeType(localEvent),
            PropertyChanges = [valueChange],
        };

        AuditEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = localEvent.Timestamp,
            UserId = currentUser.UserId ?? "system",
            UserName = currentUser.UserName,
            Category = AuditCategory.ConfigurationChange,
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            EntityChanges = [entityChange],
        };

        valueChange.AuditEntityChangeId = entityChange.Id;
        entityChange.AuditEntryId = entry.Id;

        await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static AuditChangeType DetermineChangeType(SettingChangedEvent e) => e switch
    {
        { OldValue: null, NewValue: not null } => AuditChangeType.Created,
        { OldValue: not null, NewValue: null } => AuditChangeType.Deleted,
        _ => AuditChangeType.Modified,
    };
}
