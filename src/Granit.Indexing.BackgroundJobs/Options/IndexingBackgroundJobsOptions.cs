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

    /// <summary>
    /// Upper bound on the number of entries a single rebuild run processes before
    /// checkpointing and yielding. <c>null</c> (default) leaves the run unbounded —
    /// suitable for dev and single-tenant deployments. Production multi-tenant hosts
    /// SHOULD cap it (typical: 100k–1M) so that a hostile or accidental dispatch
    /// cannot exhaust the indexer / embedding budget in one shot. When the cap is
    /// hit the service checkpoints, raises
    /// <c>RebuildBudgetExceededException</c> with <c>Reason = "max_entries_per_run"</c>,
    /// and Wolverine re-dispatches via its retry policy.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? MaxEntriesPerRun { get; set; }

    /// <summary>
    /// Wall-clock budget (seconds) for a single rebuild run. <c>null</c> (default) is
    /// unbounded. When the budget is exceeded the service checkpoints, raises
    /// <c>RebuildBudgetExceededException</c> with <c>Reason = "max_run_duration"</c>,
    /// and Wolverine re-dispatches via its retry policy. Combine with
    /// <see cref="MaxEntriesPerRun"/> to bound both per-run cost and latency.
    /// </summary>
    [Range(1, 86_400)]
    public int? MaxRunDurationSeconds { get; set; }
}
