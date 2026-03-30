namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Provides the connection string for a given tenant's isolated database.
/// Implementations are responsible for caching and credential lifecycle management.
/// </summary>
/// <remarks>
/// <para>
/// For environments backed by HashiCorp Vault dynamic credentials, implement this interface
/// using one <c>VaultCredentialLeaseManager</c> per tenant database. The lease manager
/// handles token renewal transparently — connection strings are updated in place when
/// credentials rotate.
/// </para>
/// <para>
/// For static connection strings (tests, development, pre-provisioned databases):
/// <code>
/// services.AddSingleton&lt;ITenantConnectionStringProvider&gt;(
///     new DelegateTenantConnectionStringProvider(tenantId =&gt;
///         Task.FromResult(tenantConnectionStrings[tenantId])));
/// </code>
/// </para>
/// </remarks>
public interface ITenantConnectionStringProvider
{
    /// <summary>
    /// Returns the connection string for the specified tenant's isolated database.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid connection string for the tenant's isolated database.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tenant is not registered or its credentials cannot be resolved.
    /// A missing mapping is a configuration error, not a recoverable condition.
    /// </exception>
    Task<string> GetConnectionStringAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
