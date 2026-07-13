using System.ComponentModel.DataAnnotations;

namespace Granit.RateLimiting.Options;

/// <summary>
/// Configuration options for the Granit rate limiting module.
/// Bound from <c>appsettings.json</c> section <c>"RateLimiting"</c>.
/// </summary>
public sealed class GranitRateLimitingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Whether rate limiting is enabled. Default: <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Redis key prefix for all rate limiting counters. Default: <c>rl</c>.</summary>
    [Required]
    public string KeyPrefix { get; set; } = "rl";

    /// <summary>Behavior when the counter store is unavailable. Default: <see cref="CounterStoreFailureBehavior.Deny"/>.</summary>
    public CounterStoreFailureBehavior FallbackOnCounterStoreFailure { get; set; } = CounterStoreFailureBehavior.Deny;

    /// <summary>
    /// Roles that bypass rate limiting entirely.
    /// If the current user has any of these roles, rate limiting is not applied.
    /// </summary>
    public string[] BypassRoles { get; set; } = [];

    /// <summary>Named rate limiting policies.</summary>
    public Dictionary<string, RateLimitPolicyOptions> Policies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether to use <c>Granit.Features</c> for plan-based quota resolution.</summary>
    public bool UseFeatureBasedQuotas { get; set; }

    /// <summary>
    /// Allows the in-memory counter store outside Development. Default: <see langword="false"/> —
    /// the host fails at startup instead.
    /// <para>
    /// Per-pod counters multiply every limit by the replica count (a 100 req/s policy becomes
    /// N×100 cluster-wide). Register a Redis <c>IConnectionMultiplexer</c> for cluster-wide
    /// enforcement, or opt in for genuinely single-instance deployments.
    /// </para>
    /// </summary>
    public bool AllowInMemoryCounterStore { get; set; }
}
