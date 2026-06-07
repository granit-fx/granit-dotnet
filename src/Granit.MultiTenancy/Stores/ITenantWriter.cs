namespace Granit.MultiTenancy.Stores;

/// <summary>
/// Write operations for tenant administration (CQRS command side).
/// Implementation provided by <c>Granit.MultiTenancy.EntityFrameworkCore</c>.
/// </summary>
public interface ITenantWriter
{
    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    /// <param name="id">Unique identifier for the new tenant.</param>
    /// <param name="name">Display name (max 256 characters).</param>
    /// <param name="identifier">Unique slug/subdomain identifier (max 64 characters).</param>
    /// <param name="contactEmail">Optional contact email address.</param>
    /// <param name="jurisdiction">ISO 3166 jurisdiction code, or <c>null</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(Guid id, string name, string identifier, string? contactEmail, string? jurisdiction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tenant's details.
    /// </summary>
    /// <param name="id">Tenant identifier.</param>
    /// <param name="name">New display name.</param>
    /// <param name="contactEmail">New contact email (or <c>null</c> to clear).</param>
    /// <param name="jurisdiction">ISO 3166 jurisdiction code, or <c>null</c> to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Guid id, string name, string? contactEmail, string? jurisdiction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tenant's details with optimistic concurrency check.
    /// </summary>
    /// <param name="id">Tenant identifier.</param>
    /// <param name="name">New display name.</param>
    /// <param name="contactEmail">New contact email (or <c>null</c> to clear).</param>
    /// <param name="jurisdiction">ISO 3166 jurisdiction code, or <c>null</c> to clear.</param>
    /// <param name="concurrencyStamp">Client-supplied stamp from the last read; must match the stored value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Guid id, string name, string? contactEmail, string? jurisdiction, string concurrencyStamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a tenant.
    /// </summary>
    /// <param name="id">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a tenant.
    /// </summary>
    /// <param name="id">Tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
