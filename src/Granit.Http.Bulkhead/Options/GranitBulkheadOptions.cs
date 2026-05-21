using System.ComponentModel.DataAnnotations;

namespace Granit.Http.Bulkhead.Options;

/// <summary>
/// Configuration options for the Granit bulkhead isolation module.
/// Bound from <c>appsettings.json</c> section <c>"Bulkhead"</c>.
/// </summary>
public sealed class GranitBulkheadOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Http:Bulkhead";

    /// <summary>Whether bulkhead isolation is enabled. Default: <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Roles that bypass bulkhead checks entirely.
    /// Machine actors (<see cref="Granit.Users.ActorKind.System"/>) are always bypassed regardless of this setting.
    /// </summary>
    public string[] BypassRoles { get; set; } = [];

    /// <summary>Whether to use <c>Granit.Features</c> for plan-based quota resolution.</summary>
    public bool UseFeatureBasedQuotas { get; set; }

    /// <summary>Named bulkhead policies. Each policy defines per-tenant concurrency and queue limits.</summary>
    [Required]
    public Dictionary<string, BulkheadPolicyOptions> Policies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How long an idle limiter (no active leases, no recent usage) stays in the registry
    /// before eviction. Default: 30 minutes.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Interval between cleanup sweeps. Default: 5 minutes.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Hard cap on the number of live limiters in the registry. When the cap
    /// is hit, the least-recently-used limiter is evicted synchronously to
    /// make room for a new one. Defaults to <c>10_000</c> — high enough to
    /// accommodate real multi-tenant fleets, low enough to bound worst-case
    /// memory if the idle-timeout sweeper falls behind or if attacker
    /// traffic spams unique tenant ids faster than the cleanup interval.
    /// Must be &gt;= 1.
    /// </summary>
    public int MaxLimiters { get; set; } = 10_000;
}
