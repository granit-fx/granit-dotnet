using System.ComponentModel.DataAnnotations;
using Granit.Auditing.Domain;

namespace Granit.Auditing.Options;

/// <summary>
/// Configuration options for the Granit.Auditing module.
/// Bound from the <c>"Auditing"</c> configuration section.
/// </summary>
public sealed class AuditingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auditing";

    /// <summary>
    /// Controls how audit entries are persisted after capture.
    /// <see cref="AuditPersistenceMode.Strict"/> persists synchronously (ISO 27001 strict, durable).
    /// <see cref="AuditPersistenceMode.Async"/> uses a background channel (best performance, buffered).
    /// Default: <see cref="AuditPersistenceMode.Strict"/> — secure by default; opt into
    /// <see cref="AuditPersistenceMode.Async"/> for throughput-sensitive workloads.
    /// </summary>
    public AuditPersistenceMode PersistenceMode { get; set; } = AuditPersistenceMode.Strict;

    /// <summary>
    /// Whether to capture property-level old/new values in <see cref="AuditPropertyChange"/>.
    /// When <c>false</c>, only entity-level changes are recorded.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnablePropertyTracking { get; set; } = true;

    /// <summary>
    /// Retention period for <see cref="AuditCategory.ConfigurationChange"/> entries.
    /// Default: ~7 years (2555 days).
    /// </summary>
    public TimeSpan ConfigurationChangeRetention { get; set; } = TimeSpan.FromDays(2555);

    /// <summary>
    /// Retention period for <see cref="AuditCategory.DataMutation"/> entries.
    /// Default: 365 days.
    /// </summary>
    public TimeSpan DataMutationRetention { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Retention period for <see cref="AuditCategory.DataAccess"/> entries.
    /// Default: 90 days.
    /// </summary>
    public TimeSpan DataAccessRetention { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Retention period for <see cref="AuditCategory.AccessDenied"/> entries.
    /// Default: ~7 years (2555 days).
    /// </summary>
    public TimeSpan AccessDeniedRetention { get; set; } = TimeSpan.FromDays(2555);

    /// <summary>
    /// Retention period for <see cref="AuditCategory.PrivilegedAccess"/> entries.
    /// Default: ~7 years (2555 days) — same regulatory weight as <see cref="AuditCategory.AccessDenied"/>.
    /// </summary>
    public TimeSpan PrivilegedAccessRetention { get; set; } = TimeSpan.FromDays(2555);

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
    [Range(1, 100_000)]
    public int CleanupBatchSize { get; set; } = 10_000;

    /// <summary>
    /// Maximum number of audit batches buffered in the async persistence channel.
    /// When the channel is full, producers (interceptors) wait until space is available.
    /// Only applies in <see cref="AuditPersistenceMode.Async"/> mode.
    /// Default: 10,000.
    /// </summary>
    [Range(100, 1_000_000)]
    public int ChannelCapacity { get; set; } = 10_000;

    /// <summary>
    /// Returns the retention period for the given category.
    /// </summary>
    public TimeSpan GetRetention(AuditCategory category) => category switch
    {
        AuditCategory.ConfigurationChange => ConfigurationChangeRetention,
        AuditCategory.DataMutation => DataMutationRetention,
        AuditCategory.DataAccess => DataAccessRetention,
        AuditCategory.AccessDenied => AccessDeniedRetention,
        AuditCategory.PrivilegedAccess => PrivilegedAccessRetention,
        _ => DataMutationRetention,
    };
}
