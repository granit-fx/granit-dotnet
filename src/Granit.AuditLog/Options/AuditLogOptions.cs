using Granit.AuditLog.Domain;

namespace Granit.AuditLog.Options;

/// <summary>
/// Configuration options for the Granit.AuditLog module.
/// Bound from the <c>"AuditLog"</c> configuration section.
/// </summary>
public sealed class AuditLogOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AuditLog";

    /// <summary>
    /// Controls how audit entries are persisted after capture.
    /// <see cref="AuditPersistenceMode.Async"/> uses a background channel (best performance).
    /// <see cref="AuditPersistenceMode.Strict"/> persists synchronously (ISO 27001 strict).
    /// Default: <see cref="AuditPersistenceMode.Async"/>.
    /// </summary>
    public AuditPersistenceMode PersistenceMode { get; set; } = AuditPersistenceMode.Async;

    /// <summary>
    /// Whether to capture property-level old/new values in <see cref="AuditPropertyChange"/>.
    /// When <c>false</c>, only entity-level changes are recorded.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnablePropertyTracking { get; set; } = true;

    /// <summary>
    /// Number of entries the background persistence worker processes per batch
    /// (Async mode only). Default: 50.
    /// </summary>
    public int PersistenceBatchSize { get; set; } = 50;

    /// <summary>
    /// Retention period for <see cref="AuditLogCategory.ConfigurationChange"/> entries.
    /// Default: ~7 years (2555 days).
    /// </summary>
    public TimeSpan ConfigurationChangeRetention { get; set; } = TimeSpan.FromDays(2555);

    /// <summary>
    /// Retention period for <see cref="AuditLogCategory.DataMutation"/> entries.
    /// Default: 365 days.
    /// </summary>
    public TimeSpan DataMutationRetention { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Retention period for <see cref="AuditLogCategory.DataAccess"/> entries.
    /// Default: 90 days.
    /// </summary>
    public TimeSpan DataAccessRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Retention period for <see cref="AuditLogCategory.AccessDenied"/> entries.
    /// Default: ~7 years (2555 days).
    /// </summary>
    public TimeSpan AccessDeniedRetention { get; set; } = TimeSpan.FromDays(2555);

    /// <summary>
    /// Cache duration for individual audit log entries retrieved by ID.
    /// Entries are immutable so a long TTL is safe. Default: 30 minutes.
    /// </summary>
    public TimeSpan CacheEntryTtl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Cache duration for entity-scoped audit log queries (<c>GetByEntityAsync</c>).
    /// Short TTL because new entries may be appended. Default: 2 minutes.
    /// </summary>
    public TimeSpan CacheEntityQueryTtl { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Interval between cleanup runs. Default: 24 hours.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum number of entries deleted per cleanup batch.
    /// Prevents long-running transactions. Default: 10,000.
    /// </summary>
    public int CleanupBatchSize { get; set; } = 10_000;

    /// <summary>
    /// Returns the retention period for the given category.
    /// </summary>
    public TimeSpan GetRetention(AuditLogCategory category) => category switch
    {
        AuditLogCategory.ConfigurationChange => ConfigurationChangeRetention,
        AuditLogCategory.DataMutation => DataMutationRetention,
        AuditLogCategory.DataAccess => DataAccessRetention,
        AuditLogCategory.AccessDenied => AccessDeniedRetention,
        _ => DataMutationRetention,
    };
}
