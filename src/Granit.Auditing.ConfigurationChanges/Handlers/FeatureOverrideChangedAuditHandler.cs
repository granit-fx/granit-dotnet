using Granit.Auditing.ConfigurationChanges.Internal;
using Granit.Auditing.Domain;
using Granit.Events;
using Granit.Features.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Users;

namespace Granit.Auditing.ConfigurationChanges.Handlers;

/// <summary>
/// Persists <see cref="FeatureOverrideChangedEvent"/> as an <see cref="AuditEntry"/>
/// with category <see cref="AuditCategory.ConfigurationChange"/>.
/// </summary>
public sealed class FeatureOverrideChangedAuditHandler(
    IAuditingWriter writer,
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
            OriginalValue = SensitiveValueMasker.MaskIfSensitive(localEvent.FeatureName, localEvent.OldValue),
            NewValue = SensitiveValueMasker.MaskIfSensitive(localEvent.FeatureName, localEvent.NewValue),
        };

        AuditEntityChange entityChange = new()
        {
            Id = guidGenerator.Create(),
            EntityType = "FeatureOverride",
            EntityId = localEvent.FeatureName,
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
            TenantId = localEvent.TenantId
                       ?? (currentTenant.IsAvailable ? currentTenant.Id : null),
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            EntityChanges = [entityChange],
        };

        valueChange.AuditEntityChangeId = entityChange.Id;
        entityChange.AuditEntryId = entry.Id;

        await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private static AuditChangeType DetermineChangeType(FeatureOverrideChangedEvent e) => e switch
    {
        { OldValue: null, NewValue: not null } => AuditChangeType.Created,
        { OldValue: not null, NewValue: null } => AuditChangeType.Deleted,
        _ => AuditChangeType.Modified,
    };
}
