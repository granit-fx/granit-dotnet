namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Configuration options for the static tenant isolation strategy.
/// Bound from the <c>TenantIsolation</c> section of <c>appsettings.json</c>.
/// </summary>
public sealed class TenantIsolationOptions
{
    /// <summary>
    /// The isolation strategy applied to all tenants.
    /// Defaults to <see cref="TenantIsolationStrategy.SharedDatabase"/> when the configuration
    /// section is absent.
    /// </summary>
    public TenantIsolationStrategy Strategy { get; set; } = TenantIsolationStrategy.SharedDatabase;
}
