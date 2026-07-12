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
    /// Regulatory retention floor enforced by <see cref="AuditingOptionsValidator"/> across
    /// every <see cref="AuditCategory"/>. Default: 1095 days (3 years — ISO 27001 minimum
    /// retention before physical purge).
    /// </summary>
    /// <remarks>
    /// Lowering this value below 3 years is a <b>documented compliance deviation</b>: it lets
    /// hosts that consciously accept a shorter trail configure category retentions under the
    /// ISO floor. Keep the justification next to the override
    /// (<c>Auditing__MinimumRetention</c>) in the host's configuration.
    /// </remarks>
    public TimeSpan MinimumRetention { get; set; } = TimeSpan.FromDays(1095);

    /// <summary>
    /// Whether to capture property-level old/new values in <see cref="AuditPropertyChange"/>.
    /// When <c>false</c>, only entity-level changes are recorded.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnablePropertyTracking { get; set; } = true;

    /// <summary>
    /// Retention period per audit category. Categories absent from the dictionary fall back
    /// to their built-in default (see <see cref="GetRetention"/>) — a partial override never
    /// creates a retention gap.
    /// </summary>
    /// <remarks>
    /// Defaults: <see cref="AuditCategory.ConfigurationChange"/>,
    /// <see cref="AuditCategory.AccessDenied"/> and <see cref="AuditCategory.PrivilegedAccess"/>
    /// keep ~7 years (2555 days, regulatory weight); <see cref="AuditCategory.DataMutation"/>
    /// and <see cref="AuditCategory.DataAccess"/> default to 1095 days (ISO 27001 floor).
    /// Override per category via configuration, e.g.
    /// <c>Auditing__Retention__DataAccess=365.00:00:00</c> (requires lowering
    /// <see cref="MinimumRetention"/> when going under the floor).
    /// </remarks>
    public Dictionary<AuditCategory, TimeSpan> Retention { get; set; } = new()
    {
        [AuditCategory.ConfigurationChange] = TimeSpan.FromDays(2555),
        [AuditCategory.DataMutation] = TimeSpan.FromDays(1095),
        [AuditCategory.DataAccess] = TimeSpan.FromDays(1095),
        [AuditCategory.AccessDenied] = TimeSpan.FromDays(2555),
        [AuditCategory.PrivilegedAccess] = TimeSpan.FromDays(2555),
    };

    /// <summary>
    /// Whether a GDPR Art. 17 erasure request pseudonymizes the subject's direct identifiers
    /// (user id hashed, username masked, IP and user-agent cleared) while retaining the audit
    /// events themselves (Art. 17(3)(b) + ISO 27001 A.12.4). Default: <c>true</c>.
    /// When <c>false</c>, the trail is retained as-is and pseudonymization stays a manual
    /// admin operation (<c>POST /pseudonymize/{userId}</c>).
    /// </summary>
    public bool PseudonymizeOnErasure { get; set; } = true;

    /// <summary>
    /// Salt mixed into the SHA-256 pseudonymization hash of user identifiers.
    /// Strongly recommended: an unsalted hash of a low-entropy identifier (email, username)
    /// is re-identifiable by dictionary attack. Provide via environment or Vault
    /// (<c>Auditing__PseudonymizationSalt</c>) — never in <c>appsettings.json</c>.
    /// </summary>
    /// <remarks>
    /// The salt must stay stable for the lifetime of the trail: repeated pseudonymizations of
    /// the same user converge on the same hash only under the same salt. When unset, hashing
    /// is unsalted (acceptable only when user ids are opaque GUIDs) and a warning is logged.
    /// </remarks>
    public string? PseudonymizationSalt { get; set; }

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
    /// Maximum number of entries deleted per cleanup batch.
    /// Prevents long-running transactions. Default: 10,000.
    /// </summary>
    [Range(1, 100_000)]
    public int CleanupBatchSize { get; set; } = 10_000;

    /// <summary>
    /// Returns the retention period for the given category — the configured value when
    /// present, otherwise the built-in default for that category.
    /// </summary>
    public TimeSpan GetRetention(AuditCategory category) =>
        Retention.TryGetValue(category, out TimeSpan value) ? value : DefaultRetention(category);

    private static TimeSpan DefaultRetention(AuditCategory category) => category switch
    {
        AuditCategory.ConfigurationChange => TimeSpan.FromDays(2555),
        AuditCategory.AccessDenied => TimeSpan.FromDays(2555),
        AuditCategory.PrivilegedAccess => TimeSpan.FromDays(2555),
        _ => TimeSpan.FromDays(1095),
    };
}
