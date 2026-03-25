using System.Text.Json;
using Granit.Auditing.Abstractions;
using Granit.Auditing.Attributes;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Scoped service that captures EF Core ChangeTracker state and holds it until
/// persistence is requested. The interceptor delegates all stateful work here
/// to avoid concurrency issues (interceptors can be singleton-scoped).
/// </summary>
internal sealed partial class ChangeTrackingCaptureService(
    IClock clock,
    ICurrentUserService currentUserService,
    ICurrentTenant currentTenant,
    IAuditEntryPublisher publisher,
    IOptions<AuditingOptions> options,
    ILogger<ChangeTrackingCaptureService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private const string SensitiveMask = "***";

    private AuditingBatch? _capturedBatch;

    /// <summary>
    /// Captures the current ChangeTracker state into an <see cref="AuditingBatch"/>.
    /// Call from <c>SavingChangesAsync</c>.
    /// </summary>
    public void Capture(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            List<AuditEntityChangeSnapshot> entityChanges = [];

            foreach (EntityEntry entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                {
                    continue;
                }

                Type entityType = entry.Entity.GetType();
                if (entityType.GetCustomAttributes(typeof(AuditIgnoreAttribute), true).Length > 0)
                {
                    continue;
                }

                AuditChangeType changeType = DetermineChangeType(entry);
                string entityId = GetEntityId(entry);
                List<AuditPropertyChangeSnapshot> propertyChanges = options.Value.EnablePropertyTracking
                    ? CapturePropertyChanges(entry, changeType)
                    : [];

                entityChanges.Add(new AuditEntityChangeSnapshot(
                    entityType.Name,
                    entityId,
                    changeType,
                    propertyChanges));
            }

            if (entityChanges.Count == 0)
            {
                _capturedBatch = null;
                return;
            }

            _capturedBatch = new AuditingBatch(
                Timestamp: clock.Now,
                UserId: currentUserService.UserId ?? "system",
                UserName: currentUserService.UserName,
                Category: AuditCategory.DataMutation,
                IpAddress: null,
                UserAgent: null,
                TenantId: currentTenant.IsAvailable ? currentTenant.Id : null,
                CorrelationId: System.Diagnostics.Activity.Current?.Id,
                EntityChanges: entityChanges);
        }
        catch (Exception ex)
        {
            LogCaptureError(ex);
            _capturedBatch = null;
        }
    }

    /// <summary>
    /// Publishes the previously captured batch. Call from <c>SavedChangesAsync</c>
    /// only if save succeeded.
    /// </summary>
    public async ValueTask PublishAsync(CancellationToken cancellationToken = default)
    {
        if (_capturedBatch is null)
        {
            return;
        }

        AuditingBatch batch = _capturedBatch;
        _capturedBatch = null;

        await publisher.PublishAsync(batch, cancellationToken).ConfigureAwait(false);
    }

    private static AuditChangeType DetermineChangeType(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditChangeType.Created,
        EntityState.Deleted => AuditChangeType.Deleted,
        EntityState.Modified when IsSoftDeleted(entry) => AuditChangeType.SoftDeleted,
        _ => AuditChangeType.Modified,
    };

    private static bool IsSoftDeleted(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable)
        {
            return false;
        }

        PropertyEntry? isDeletedProp = entry.Properties
            .FirstOrDefault(p => p.Metadata.Name == nameof(ISoftDeletable.IsDeleted));

        return isDeletedProp is not null
            && isDeletedProp.OriginalValue is false
            && isDeletedProp.CurrentValue is true;
    }

    private static string GetEntityId(EntityEntry entry)
    {
        PropertyEntry? idProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return idProp?.CurrentValue?.ToString() ?? string.Empty;
    }

    private static List<AuditPropertyChangeSnapshot> CapturePropertyChanges(
        EntityEntry entry,
        AuditChangeType changeType)
    {
        List<AuditPropertyChangeSnapshot> changes = [];

        foreach (PropertyEntry prop in entry.Properties.Where(p => !p.Metadata.IsPrimaryKey()))
        {
            AuditPropertyChangeSnapshot? snapshot = CapturePropertySnapshot(prop, changeType);
            if (snapshot is not null)
            {
                changes.Add(snapshot);
            }
        }

        return changes;
    }

    private static AuditPropertyChangeSnapshot? CapturePropertySnapshot(
        PropertyEntry prop,
        AuditChangeType changeType)
    {
        bool isSensitive = prop.Metadata.PropertyInfo?
            .GetCustomAttributes(typeof(AuditSensitiveAttribute), true).Length > 0;

        return changeType switch
        {
            AuditChangeType.Created => new AuditPropertyChangeSnapshot(
                prop.Metadata.Name,
                null,
                isSensitive ? SensitiveMask : SerializeValue(prop.CurrentValue)),

            AuditChangeType.Deleted => new AuditPropertyChangeSnapshot(
                prop.Metadata.Name,
                isSensitive ? SensitiveMask : SerializeValue(prop.OriginalValue),
                null),

            _ when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue) =>
                new AuditPropertyChangeSnapshot(
                    prop.Metadata.Name,
                    isSensitive ? SensitiveMask : SerializeValue(prop.OriginalValue),
                    isSensitive ? SensitiveMask : SerializeValue(prop.CurrentValue)),

            _ => null,
        };
    }

    private static string? SerializeValue(object? value) => value switch
    {
        null => null,
        string s => s,
        DateTime or DateTimeOffset or Guid or bool => value.ToString(),
        _ when value.GetType().IsPrimitive => value.ToString(),
        Enum e => e.ToString(),
        _ => JsonSerializer.Serialize(value, JsonOptions),
    };

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to capture audit change tracking data")]
    private partial void LogCaptureError(Exception exception);
}
