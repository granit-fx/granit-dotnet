namespace Granit.MultiTenancy.Stores;

/// <summary>
/// Read operations for tenant administration (CQRS query side).
/// Implementation provided by <c>Granit.MultiTenancy.EntityFrameworkCore</c>.
/// </summary>
public interface ITenantReader
{
    /// <summary>
    /// Finds a tenant by its unique identifier.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant data, or <c>null</c> if not found.</returns>
    Task<TenantData?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a tenant by its slug/subdomain identifier.
    /// </summary>
    /// <param name="identifier">The unique slug/subdomain identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant data, or <c>null</c> if not found.</returns>
    Task<TenantData?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a tenant by its custom domain.
    /// Used by <see cref="Resolvers.CustomDomainTenantResolver"/> for inbound resolution.
    /// </summary>
    /// <param name="customDomain">The custom domain to look up (e.g., <c>"app.acme-corp.com"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant data, or <c>null</c> if no tenant uses this domain.</returns>
    Task<TenantData?> FindByCustomDomainAsync(string customDomain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All tenant data entries.</returns>
    Task<IReadOnlyList<TenantData>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a tenant with the given identifier exists.
    /// </summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the tenant exists.</returns>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
