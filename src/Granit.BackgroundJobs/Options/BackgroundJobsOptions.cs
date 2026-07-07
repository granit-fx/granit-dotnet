namespace Granit.BackgroundJobs.Options;

/// <summary>
/// Configuration options for the Granit background jobs module.
/// Bound from the <c>"BackgroundJobs"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The persistence strategy is decided by package composition, not configuration:
/// the default store is in-memory (state lost on restart — development and tests);
/// adding <c>Granit.BackgroundJobs.EntityFrameworkCore</c> replaces it with a durable
/// EF Core store (SQL Server / PostgreSQL).
/// </remarks>
public sealed class BackgroundJobsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// Number of consecutive handler failures after which a
    /// <see cref="Events.BackgroundJobFailureThresholdExceededEto"/> is published
    /// (a single alert, re-armed by the next successful execution). Default: 3.
    /// </summary>
    public int FailureAlertThreshold { get; set; } = 3;
}
