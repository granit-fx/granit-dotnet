using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Granit.Auditing.Attributes;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.DataProtection;
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
    AuditingMetrics metrics,
    Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor,
    ILogger<ChangeTrackingCaptureService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly ConcurrentDictionary<Type, EntityAuditMetadata> MetadataCache = new();

    private const string SensitiveMask = "***";
    private const int MaxIpAddressLength = 45;
    private const int MaxUserAgentLength = 500;

    private AuditingBatch? _capturedBatch;

    /// <summary>
    /// Captures the current ChangeTracker state into an <see cref="AuditingBatch"/>.
    /// Call from <c>SavingChangesAsync</c>.
    /// </summary>
    public void Capture(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using System.Diagnostics.Activity? activity = AuditingActivitySource.Source.StartActivity(AuditingActivitySource.Capture);
        activity?.SetTag("tenant_id", currentTenant.IsAvailable ? currentTenant.Id?.ToString() ?? "global" : "global");

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
                EntityAuditMetadata metadata = GetOrCreateMetadata(entityType);

                if (metadata.IsIgnored)
                {
                    continue;
                }

                AuditChangeType changeType = DetermineChangeType(entry);
                string entityId = GetEntityId(entry);
                List<AuditPropertyChangeSnapshot> propertyChanges = options.Value.EnablePropertyTracking
                    ? CapturePropertyChanges(entry, changeType, metadata)
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

            Microsoft.AspNetCore.Http.HttpContext? httpContext = httpContextAccessor?.HttpContext;

            _capturedBatch = new AuditingBatch(
                Timestamp: clock.Now,
                UserId: currentUserService.UserId ?? "system",
                UserName: currentUserService.UserName,
                Category: AuditCategory.DataMutation,
                IpAddress: Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), MaxIpAddressLength),
                UserAgent: Truncate(httpContext?.Request.Headers.UserAgent.ToString(), MaxUserAgentLength),
                TenantId: currentTenant.IsAvailable ? currentTenant.Id : null,
                CorrelationId: System.Diagnostics.Activity.Current?.Id,
                EntityChanges: entityChanges);

            activity?.SetTag("audit.category", _capturedBatch.Category.ToString());
            activity?.SetTag("audit.entity_change_count", entityChanges.Count);
        }
        catch (Exception ex)
        {
            LogCaptureError(ex);
            metrics.RecordCaptureError(currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null);
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

    private static EntityAuditMetadata GetOrCreateMetadata(Type entityType) =>
        MetadataCache.GetOrAdd(entityType, static type =>
        {
            bool isIgnored = type.GetCustomAttributes(typeof(AuditIgnoreAttribute), true).Length > 0;

            Dictionary<string, PropertyAuditMetadata> properties = new(StringComparer.Ordinal);

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool propertyIgnored = property.IsDefined(typeof(AuditIgnoreAttribute), true);

                SensitiveDataAttribute? sensitive = property
                    .GetCustomAttributes(typeof(SensitiveDataAttribute), true)
                    .OfType<SensitiveDataAttribute>()
                    .FirstOrDefault();

                properties[property.Name] = new PropertyAuditMetadata(propertyIgnored, sensitive);
            }

            return new EntityAuditMetadata(isIgnored, properties);
        });

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
        AuditChangeType changeType,
        EntityAuditMetadata metadata)
    {
        List<AuditPropertyChangeSnapshot> changes = [];

        foreach (PropertyEntry prop in entry.Properties.Where(p => !p.Metadata.IsPrimaryKey()))
        {
            string propertyName = prop.Metadata.Name;

            if (metadata.Properties.TryGetValue(propertyName, out PropertyAuditMetadata? propMeta)
                && propMeta.IsIgnored)
            {
                continue;
            }

            SensitiveDataAttribute? sensitive = propMeta?.SensitiveData;

            AuditPropertyChangeSnapshot? snapshot = CapturePropertySnapshot(prop, changeType, sensitive);
            if (snapshot is not null)
            {
                changes.Add(snapshot);
            }
        }

        return changes;
    }

    private static AuditPropertyChangeSnapshot? CapturePropertySnapshot(
        PropertyEntry prop,
        AuditChangeType changeType,
        SensitiveDataAttribute? sensitive)
    {
        return changeType switch
        {
            AuditChangeType.Created => new AuditPropertyChangeSnapshot(
                prop.Metadata.Name,
                null,
                ProtectValue(prop.CurrentValue, sensitive)),

            AuditChangeType.Deleted => new AuditPropertyChangeSnapshot(
                prop.Metadata.Name,
                ProtectValue(prop.OriginalValue, sensitive),
                null),

            _ when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue) =>
                new AuditPropertyChangeSnapshot(
                    prop.Metadata.Name,
                    ProtectValue(prop.OriginalValue, sensitive),
                    ProtectValue(prop.CurrentValue, sensitive)),

            _ => null,
        };
    }

    private static string? ProtectValue(object? value, SensitiveDataAttribute? sensitive)
    {
        if (sensitive is null)
        {
            return SerializeValue(value);
        }

        return sensitive.Mode switch
        {
            SensitiveDataMode.Omit => null,
            SensitiveDataMode.Hash => HashValue(SerializeValue(value)),
            _ => SensitiveMask,
        };
    }

    private static string? HashValue(string? value)
    {
        if (value is null)
        {
            return null;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
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

    private static string? Truncate(string? value, int maxLength) =>
        value?.Length > maxLength ? value[..maxLength] : value;

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Failed to capture audit change tracking data")]
    private partial void LogCaptureError(Exception exception);

    /// <summary>
    /// Cached audit metadata for an entity type: whether the type is ignored,
    /// and per-property ignore/sensitive-data information.
    /// </summary>
    internal sealed record EntityAuditMetadata(
        bool IsIgnored,
        Dictionary<string, PropertyAuditMetadata> Properties);

    /// <summary>
    /// Cached audit metadata for a single property.
    /// </summary>
    internal sealed record PropertyAuditMetadata(
        bool IsIgnored,
        SensitiveDataAttribute? SensitiveData);
}
