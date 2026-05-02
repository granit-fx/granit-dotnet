using System.Text.Json;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;

namespace Granit.Entities.Customization.Endpoints.Internal;

/// <summary>
/// Writes ISO 27001-compatible audit entries for customization mutations
/// (ADR-053 §Audit trail). The delta vocabulary is human-readable, so the
/// audit JSON captures user intent rather than opaque entity diffs.
/// </summary>
internal sealed class EntityCustomizationAuditWriter(
    IAuditingWriter writer,
    ICurrentUserService currentUser,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteUpsertAsync(
        EntityCustomization newState,
        IReadOnlyList<LayoutDelta>? previousDeltas,
        CancellationToken cancellationToken)
    {
        await WriteAsync(
            entityName: newState.EntityName,
            layoutKind: newState.LayoutKind,
            previousDeltas: previousDeltas,
            newDeltas: newState.Deltas,
            tenantId: newState.TenantId,
            changeType: previousDeltas is null ? AuditChangeType.Created : AuditChangeType.Modified,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteDeleteAsync(
        EntityCustomization removed,
        CancellationToken cancellationToken)
    {
        await WriteAsync(
            entityName: removed.EntityName,
            layoutKind: removed.LayoutKind,
            previousDeltas: removed.Deltas,
            newDeltas: null,
            tenantId: removed.TenantId,
            changeType: AuditChangeType.Deleted,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync(
        string entityName,
        LayoutKind layoutKind,
        IReadOnlyList<LayoutDelta>? previousDeltas,
        IReadOnlyList<LayoutDelta>? newDeltas,
        Guid? tenantId,
        AuditChangeType changeType,
        CancellationToken cancellationToken)
    {
        AuditPropertyChange deltasChange = new()
        {
            Id = guidGenerator.Create(),
            PropertyName = "Deltas",
            OriginalValue = previousDeltas is null ? null : JsonSerializer.Serialize(previousDeltas, JsonOptions),
            NewValue = newDeltas is null ? null : JsonSerializer.Serialize(newDeltas, JsonOptions),
        };

        AuditEntityChange entityChange = new()
        {
            Id = guidGenerator.Create(),
            EntityType = $"Granit.Entities.Customization:{entityName}",
            EntityId = layoutKind.ToString(),
            ChangeType = changeType,
            PropertyChanges = [deltasChange],
        };

        AuditEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = clock.Normalize(clock.Now),
            UserId = currentUser.UserId ?? "system",
            UserName = currentUser.UserName,
            Category = AuditCategory.ConfigurationChange,
            TenantId = tenantId ?? (currentTenant.IsAvailable ? currentTenant.Id : null),
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            EntityChanges = [entityChange],
        };

        deltasChange.AuditEntityChangeId = entityChange.Id;
        entityChange.AuditEntryId = entry.Id;

        await writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
