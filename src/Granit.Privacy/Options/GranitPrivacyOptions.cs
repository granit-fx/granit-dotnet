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
    /// Timeout in minutes for the GDPR export Saga.
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
}
