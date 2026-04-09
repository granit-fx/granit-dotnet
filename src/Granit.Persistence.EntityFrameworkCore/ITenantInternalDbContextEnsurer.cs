namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Creates tables for tenant-level internal <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// instances (Templating, Timeline, BlobStorage, DataExchange, QueryEngine, Webhooks, Notifications).
/// </summary>
/// <remarks>
/// <para>
/// Tenant ensurers create tables <b>per tenant</b>. In <c>SchemaPerTenant</c> mode,
/// tables are created in each tenant's dedicated schema. In <c>DatabasePerTenant</c> mode,
/// tables are created in each tenant's dedicated database. In <c>SharedDatabase</c> mode,
/// tables are created once in the default schema (same as host ensurers).
/// </para>
/// <para>
/// Discovered and executed by the migration runner after data seeding (post-seed
/// per-tenant pass) and by <c>TenantSchemaProvisioner</c> during hot provisioning.
/// </para>
/// </remarks>
public interface ITenantInternalDbContextEnsurer
{
    /// <summary>Display name for logging purposes.</summary>
    string ContextName { get; }

    /// <summary>
    /// Creates the internal DbContext tables for a specific tenant if they do not already exist.
    /// The caller must ensure the tenant context is active (<see cref="Granit.MultiTenancy.ICurrentTenant"/>).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureCreatedForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
