using Granit.Auditing.Domain;

namespace Granit.Auditing.Notifications.Options;

/// <summary>
/// Configuration for the auditing notification bridge.
/// Bound to <c>Auditing:Notifications</c> configuration section.
/// </summary>
/// <remarks>
/// The bridge subscribes to <see cref="Auditing.Events.AuditEntryPersistedEto"/> — every
/// successful audit entry. Most of those entries (regular CRUD, configuration changes,
/// data reads when enabled) are *not* anomalies and must NOT page an administrator.
/// <see cref="AlertableCategories"/> filters the firehose down to the categories the
/// SOC actually cares about. The default set targets ISO 27001 A.12.4.1 "events
/// indicating a possible security incident" — primarily authorization failures.
/// Hosts can override the list per environment (a staging cluster may want
/// <see cref="AuditCategory.ConfigurationChange"/> alerting; production typically
/// reserves it for <see cref="AuditCategory.AccessDenied"/>).
/// </remarks>
public sealed class AuditNotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auditing:Notifications";

    /// <summary>
    /// Audit categories that trigger an administrator notification when persisted.
    /// Entries whose <see cref="AuditCategory"/> is not in this list are ignored by
    /// the bridge.
    /// </summary>
    /// <remarks>
    /// Default: <see cref="AuditCategory.AccessDenied"/> and
    /// <see cref="AuditCategory.ConfigurationChange"/>. Authorization failures are the
    /// canonical security signal (A.9.4); configuration changes capture privileged
    /// settings/feature-flag tampering that warrants a four-eyes review.
    /// </remarks>
    public IReadOnlyList<AuditCategory> AlertableCategories { get; set; } =
    [
        AuditCategory.AccessDenied,
        AuditCategory.ConfigurationChange,
    ];
}
