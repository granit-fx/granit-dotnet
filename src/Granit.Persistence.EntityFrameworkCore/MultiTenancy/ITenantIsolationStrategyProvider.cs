namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Resolves the <see cref="TenantIsolationStrategy"/> to apply for a given tenant.
/// </summary>
/// <remarks>
/// Implement this interface to route specific tenants to different isolation strategies
/// (e.g., premium tenants to <see cref="TenantIsolationStrategy.DatabasePerTenant"/>,
/// standard tenants to <see cref="TenantIsolationStrategy.SharedDatabase"/>).
/// The default implementation <see cref="ConfigurationTenantIsolationStrategyProvider"/>
/// returns a single statically configured strategy for all tenants.
/// </remarks>
public interface ITenantIsolationStrategyProvider
{
    /// <summary>
    /// Gets the isolation strategy for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The current tenant identifier, or <c>null</c> when no tenant is active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="TenantIsolationStrategy"/> to apply.</returns>
    ValueTask<TenantIsolationStrategy> GetStrategyAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
