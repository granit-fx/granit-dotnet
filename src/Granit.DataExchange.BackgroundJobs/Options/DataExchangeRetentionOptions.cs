namespace Granit.DataExchange.BackgroundJobs.Options;

/// <summary>
/// Configuration options for the <c>Granit.DataExchange</c> GDPR retention sweep.
/// </summary>
/// <remarks>
/// Validated at startup by <see cref="DataExchangeRetentionOptionsValidator"/> — <see cref="TimeSpan"/>
/// values can't be expressed with <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>.
/// </remarks>
public sealed class DataExchangeRetentionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DataExchange:Retention";

    /// <summary>
    /// How long an uploaded import file is kept after the job reaches a terminal state before the
    /// sweep purges it from blob storage. GDPR storage limitation (Art. 5(1)(e)): uploaded files
    /// often carry personal data (the raw source rows) and must not be retained indefinitely once
    /// the import outcome is known. Default: 30 days.
    /// </summary>
    public TimeSpan ImportFileRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a generated export file is kept after completion before the sweep purges it from
    /// blob storage. Shorter than <see cref="ImportFileRetention"/> by default: an export is a
    /// derived, on-demand artifact (typically re-downloadable within days), so a long retention
    /// window only prolongs unnecessary exposure of the personal data it contains. Default: 7 days.
    /// </summary>
    public TimeSpan ExportFileRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a terminal job's database row (metadata, not the file) is kept before the sweep
    /// hard-deletes it. Bounds how long import/export history — which can itself carry personal
    /// data via file names, error messages, or request filters — remains queryable. Default: 365 days.
    /// </summary>
    public TimeSpan JobRecordRetention { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// How long a job may remain in a non-terminal executing state before the sweep treats it as
    /// stranded (crashed worker, lost message) and force-fails it. Without this safety net, a
    /// stranded job's uploaded/generated file would never become eligible for the retention
    /// purge above, since <c>Executing</c>/<c>Exporting</c> is never a terminal state.
    /// Default: 6 hours.
    /// </summary>
    public TimeSpan StuckJobTimeout { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Maximum number of jobs processed per category, per sweep run. Bounds a single run's duration
    /// and database/blob-storage load; the next scheduled run picks up any remainder. Default: 500.
    /// </summary>
    public int SweepBatchSize { get; set; } = 500;
}
