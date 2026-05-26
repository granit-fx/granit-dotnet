using System.ComponentModel.DataAnnotations;

namespace Granit.Indexing.BackgroundJobs.Options;

/// <summary>
/// Configuration options for <c>Granit.Indexing.BackgroundJobs</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class IndexingBackgroundJobsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Indexing:BackgroundJobs";

    /// <summary>
    /// Number of keys processed between checkpoint persistence calls. Trades crash-
    /// recovery granularity (smaller = lose less on crash) against I/O overhead on the
    /// checkpoint store (smaller = more writes). Default <c>100</c>.
    /// </summary>
    [Range(1, 10_000)]
    public int CheckpointBatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum number of consecutive per-key failures the rebuild service tolerates
    /// before aborting the whole run. Default <c>50</c> — enough to survive a transient
    /// upstream outage without blasting the metric channel during a true broken-data
    /// rollout.
    /// </summary>
    [Range(1, 1_000_000)]
    public int MaxConsecutiveFailures { get; set; } = 50;
}
