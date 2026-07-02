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
    /// Maximum size in megabytes for the export archive (legacy single-archive cap,
    /// honoured by the staging-fragment flow). Sharded assembly uses
    /// <see cref="ExportShardMaxSizeMb"/> per-shard and is bounded only by the host's
    /// storage budget.
    /// Default: 100 MB.
    /// </summary>
    [Range(1, 500)]
    public int ExportMaxSizeMb { get; set; } = 100;

    /// <summary>
    /// Maximum compressed bytes per shard before the sharded assembler rolls over
    /// to a new archive. Default 2 GB — Takeout-comparable. A single export entry
    /// larger than this cap is allowed to land alone in its own shard (ZIP64
    /// covers it) rather than being split mid-stream.
    /// </summary>
    [Range(64, 51_200)]
    public int ExportShardMaxSizeMb { get; set; } = 2_048;

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

    /// <summary>
    /// Window, in minutes, the deletion saga waits for every registered provider to acknowledge
    /// erasure (via <c>PersonalDataDeletedEto</c>) after the deadline is reached. If a provider
    /// has not acknowledged when this elapses, the request is marked
    /// <see cref="DataDeletion.DeletionRequestState.PartiallyExecuted"/> and the missing providers
    /// are surfaced for operator reconciliation. Sized in hours by default because provider
    /// erasure can be heavy (re-indexing, blob purges) and Wolverine may retry a failing provider
    /// several times before it succeeds or dead-letters. Default: 720 minutes (12 hours).
    /// </summary>
    [Range(1, 43_200)]
    public int DeletionAcknowledgementTimeoutMinutes { get; set; } = 720;

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
