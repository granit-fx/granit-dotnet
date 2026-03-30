namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Resolves the PostgreSQL schema name for a given tenant.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation (<see cref="DefaultTenantSchemaProvider"/>) derives the
/// schema name from the tenant identifier using the convention configured in
/// <see cref="TenantSchemaOptions"/>. Register a custom implementation to override
/// the naming logic (e.g., look up the schema name from a tenant catalogue).
/// </para>
/// </remarks>
public interface ITenantSchemaProvider
{
    /// <summary>
    /// Returns the PostgreSQL schema name for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The schema name to use in <c>SET search_path</c> for this tenant.
    /// Must be a valid PostgreSQL identifier (lower-case letters, digits, underscores).
    /// </returns>
    ValueTask<string> GetSchemaNameAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
