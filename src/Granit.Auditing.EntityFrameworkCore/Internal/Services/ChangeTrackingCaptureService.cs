using System.Collections.Concurrent;
using System.Diagnostics;
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
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Scoped service that captures EF Core ChangeTracker state and stages it through the
/// <see cref="AuditPersistencePipeline"/>. The interceptor delegates all stateful work here
/// to avoid concurrency issues (interceptors can be singleton-scoped).
/// </summary>
/// <remarks>
/// Staged state is keyed by <c>DbContext.ContextId.InstanceId</c> so nested saves (the
/// standalone fallback persisting through the isolated <see cref="AuditingDbContext"/>
/// re-enters this service) and multi-context scopes cannot cross-contaminate.
/// </remarks>
internal sealed partial class ChangeTrackingCaptureService(
    IClock clock,
    ICurrentUserService currentUserService,
    ICurrentTenant currentTenant,
    AuditPersistencePipeline pipeline,
    IGuidGenerator guidGenerator,
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

    /// <summary>One-shot degraded-mode warning per host context type (process-wide).</summary>
    private static readonly ConcurrentDictionary<Type, byte> StandaloneWarnedContexts = new();

    private const string SensitiveMask = "***";
    private const int MaxIpAddressLength = 45;
    private const int MaxUserAgentLength = 500;

    private readonly Dictionary<Guid, StagedAudit> _staged = [];

    /// <summary>
    /// Captures the current ChangeTracker state, maps it to an <see cref="AuditEntry"/> and
    /// stages it: into the host context's own transaction when the host model maps the audit
    /// entities (embedded mode), otherwise held for post-commit standalone persistence.
    /// Call from <c>SavingChanges(Async)</c>.
    /// </summary>
    public async ValueTask CaptureAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Defensive: a previous save on this context that failed without the failure hook
        // firing must never leak its staged entry into this save.
        _staged.Remove(context.ContextId.InstanceId);

        AuditingBatch? batch = Capture(context);
        if (batch is null)
        {
            return;
        }

        AuditEntry entry = AuditingBatchMapper.ToEntity(batch, guidGenerator);
        bool embedded = context.Model.FindEntityType(typeof(AuditEntry)) is not null;

        if (embedded)
        {
            await pipeline.StageAsync(context, entry, cancellationToken).ConfigureAwait(false);
        }
        else if (StandaloneWarnedContexts.TryAdd(context.GetType(), 0))
        {
            LogStandaloneMode(context.GetType().Name);
        }

        _staged[context.ContextId.InstanceId] = new StagedAudit(entry, embedded, Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Completes the staged entry after a successful save: records metrics (embedded mode)
    /// or persists through the isolated <see cref="AuditingDbContext"/> (standalone mode).
    /// Call from <c>SavedChanges(Async)</c>.
    /// </summary>
    public async ValueTask OnSavedAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_staged.Remove(context.ContextId.InstanceId, out StagedAudit? staged))
        {
            return;
        }

        if (staged.Embedded)
        {
            pipeline.OnCommitted(staged.Entry, Stopwatch.GetElapsedTime(staged.StartTimestamp));
            return;
        }

        await pipeline.PersistAsync(staged.Entry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the staged entry after a failed save. In embedded mode the audit graph is also
    /// detached from the host ChangeTracker — without this, a business retry on the same
    /// scope (e.g. after <c>DbUpdateConcurrencyException</c>) would re-save the stale audit
    /// rows on top of the fresh capture. Call from <c>SaveChangesFailed(Async)</c>.
    /// </summary>
    public void OnSaveFailed(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_staged.Remove(context.ContextId.InstanceId, out StagedAudit? staged) || !staged.Embedded)
        {
            return;
        }

        // Snapshot the navigations before detaching: detaching a child triggers EF
        // relationship fixup, which removes it from the very collection being iterated.
        foreach (AuditEntityChange entityChange in staged.Entry.EntityChanges.ToList())
        {
            foreach (AuditPropertyChange propertyChange in entityChange.PropertyChanges.ToList())
            {
                context.Entry(propertyChange).State = EntityState.Detached;
            }

            context.Entry(entityChange).State = EntityState.Detached;
        }

        context.Entry(staged.Entry).State = EntityState.Detached;
    }

    private AuditingBatch? Capture(DbContext context)
    {
        using Activity? activity = AuditingActivitySource.Source.StartActivity(AuditingActivitySource.Capture);
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
                return null;
            }

            Microsoft.AspNetCore.Http.HttpContext? httpContext = httpContextAccessor?.HttpContext;

            AuditingBatch batch = new(
                Timestamp: clock.Now,
                UserId: currentUserService.UserId ?? "system",
                UserName: currentUserService.UserName,
                Category: AuditCategory.DataMutation,
                IpAddress: Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), MaxIpAddressLength),
                UserAgent: Truncate(httpContext?.Request.Headers.UserAgent.ToString(), MaxUserAgentLength),
                TenantId: currentTenant.IsAvailable ? currentTenant.Id : null,
                CorrelationId: System.Diagnostics.Activity.Current?.Id,
                EntityChanges: entityChanges);

            activity?.SetTag("audit.category", batch.Category.ToString());
            activity?.SetTag("audit.entity_change_count", entityChanges.Count);

            return batch;
        }
        catch (Exception ex)
        {
            LogCaptureError(ex);
            metrics.RecordCaptureError(currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null);
            return null;
        }
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

        return isDeletedProp?.OriginalValue is false
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Audit persistence for {ContextType} runs in standalone mode — the audit row is written after the business commit, in its own transaction (not atomic). Map the audit tables into the context via modelBuilder.ConfigureAuditingModule() for atomic auditing.")]
    private partial void LogStandaloneMode(string contextType);

    /// <summary>
    /// A captured audit entry awaiting save completion: staged into the host transaction
    /// (embedded) or pending standalone persistence after the host commit.
    /// </summary>
    private sealed record StagedAudit(AuditEntry Entry, bool Embedded, long StartTimestamp);

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
