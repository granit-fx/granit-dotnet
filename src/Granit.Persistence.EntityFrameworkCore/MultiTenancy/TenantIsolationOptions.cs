namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Configuration options for the static tenant isolation strategy.
/// Bound from the <c>MultiTenancy:TenantIsolation</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class TenantIsolationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MultiTenancy:TenantIsolation";

    /// <summary>
    /// The isolation strategy applied to all tenants.
    /// Defaults to <see cref="TenantIsolationStrategy.SharedDatabase"/> when the configuration
    /// section is absent.
    /// </summary>
    public TenantIsolationStrategy Strategy { get; set; } = TenantIsolationStrategy.SharedDatabase;

    /// <summary>
    /// Database schema for host-level tables (identity, audit, tenants, background jobs…).
    /// When set, host module <c>*DbProperties</c> classes use this schema via
    /// <see cref="GranitDbDefaults.HostDbSchema"/>. Default: <c>null</c> (provider default).
    /// </summary>
    public string? HostSchema { get; set; }
}
