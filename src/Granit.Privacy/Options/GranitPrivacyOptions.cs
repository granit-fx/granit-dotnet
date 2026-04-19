using System.ComponentModel.DataAnnotations;

namespace Granit.Privacy.Options;

/// <summary>
/// Configuration options for the Granit.Privacy module.
/// </summary>
public sealed class GranitPrivacyOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Privacy";

    /// <summary>
    /// Timeout in minutes for the privacy export Saga.
    /// If not all providers respond within this time, a partial export is generated.
    /// Default: 5 minutes.
    /// </summary>
    [Range(1, 60)]
    public int ExportTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum size in megabytes for the export archive.
    /// Default: 100 MB.
    /// </summary>
    [Range(1, 500)]
    public int ExportMaxSizeMb { get; set; } = 100;

    /// <summary>
    /// Time-to-live, in minutes, of the presigned URLs that the archive assembler
    /// requests to download each fragment.
    /// </summary>
    /// <remarks>
    /// The assembler is triggered by <c>ExportCompletedEto</c>, which fires at the end
    /// of the saga — up to <see cref="ExportTimeoutMinutes"/> after the earliest fragment
    /// was uploaded, plus Wolverine retry headroom. The default (15 min) covers a 5-minute
    /// saga timeout with a comfortable retry budget. Blob providers may enforce a lower
    /// ceiling — the effective TTL is the minimum of the two.
    /// Default: 15 minutes.
    /// </remarks>
    [Range(1, 60)]
    public int ArchiveAssemblyDownloadUrlExpiryMinutes { get; set; } = 15;

    // ── Deletion cooling-off period ──────────────────────────────────────────

    /// <summary>
    /// Default grace period in days when a user defers deletion.
    /// Default: 30 days.
    /// </summary>
    [Range(1, 365)]
    public int DefaultGracePeriodDays { get; set; } = 30;

    /// <summary>
    /// Maximum allowed grace period in days (CNIL guidance: "reasonable delay").
    /// Default: 90 days.
    /// </summary>
    [Range(1, 365)]
    public int MaxGracePeriodDays { get; set; } = 90;

    /// <summary>
    /// Number of days before the deletion deadline to send a reminder notification.
    /// Default: 3 days. Set to 0 to disable reminders.
    /// </summary>
    [Range(0, 30)]
    public int ReminderDaysBefore { get; set; } = 3;

    // ── Per-regulation overrides ────────────────────────────────────────────

    /// <summary>
    /// Per-regulation overrides for timeline and deletion settings.
    /// Key: regulation code (e.g., <c>"BR_LGPD"</c>). Values override the global defaults above.
    /// When <c>Granit.Privacy.Regulations</c> is loaded, the regulation profile provides the defaults
    /// and these overrides take precedence over profile values.
    /// </summary>
    public Dictionary<string, PrivacyRegulationOverrides> RegulationOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Per-regulation option overrides. Null properties fall back to the regulation profile defaults,
/// then to the global <see cref="GranitPrivacyOptions"/> defaults.
/// </summary>
public sealed class PrivacyRegulationOverrides
{
    /// <summary>Override for the default grace period (calendar days).</summary>
    public int? DefaultGracePeriodDays { get; set; }

    /// <summary>Override for the maximum grace period (calendar days).</summary>
    public int? MaxGracePeriodDays { get; set; }

    /// <summary>Override for the export timeout (minutes).</summary>
    public int? ExportTimeoutMinutes { get; set; }

    /// <summary>Override for the reminder lead time (days before deadline).</summary>
    public int? ReminderDaysBefore { get; set; }
}
