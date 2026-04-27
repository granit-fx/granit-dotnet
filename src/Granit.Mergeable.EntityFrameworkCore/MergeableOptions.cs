using System.ComponentModel.DataAnnotations;

namespace Granit.Mergeable.EntityFrameworkCore;

/// <summary>
/// Host-tunable knobs for the merge orchestrator. Bound from the <c>Mergeable</c> section in
/// <c>appsettings.json</c> via <c>BindConfiguration("Mergeable")</c> in the DI extension.
/// Defaults are conservative — match a typical Postgres single-host deployment.
/// </summary>
public sealed class MergeableOptions
{
    /// <summary>Configuration section name (<c>Mergeable</c>).</summary>
    public const string SectionName = "Mergeable";

    /// <summary>
    /// Hard ceiling on the merge transaction. Defaults to 30 seconds — long enough to merge a
    /// party with thousands of children + cross-module rewriters, short enough to fail fast on
    /// stuck rewriters before they hold the per-tenant advisory lock indefinitely.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:30:00")]
    public TimeSpan MergeTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retention window for cached idempotency-key entries. Past this age, the recurring
    /// cleanup job (<c>Granit.Mergeable.BackgroundJobs.MergeIdempotencyCleanupJob</c>) deletes
    /// the row. Defaults to 24 hours, matching the documented Stripe-style replay contract.
    /// </summary>
    [Range(typeof(TimeSpan), "00:30:00", "30.00:00:00")]
    public TimeSpan IdempotencyRetention { get; set; } = TimeSpan.FromHours(24);
}
