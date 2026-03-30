namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Defines the data isolation strategy used to separate tenant data.
/// </summary>
public enum TenantIsolationStrategy
{
    /// <summary>
    /// All tenants share a single database and schema. Isolation is enforced by a global
    /// EF Core query filter on <c>TenantId</c>.
    /// </summary>
    SharedDatabase,

    /// <summary>
    /// Each tenant has a dedicated PostgreSQL database. The connection string is resolved
    /// per request via <see cref="ITenantConnectionStringProvider"/>.
    /// </summary>
    DatabasePerTenant,

    /// <summary>
    /// All tenants share a single database, but each tenant has a dedicated PostgreSQL schema.
    /// The active schema is set via <c>SET search_path</c> unconditionally at every connection open.
    /// </summary>
    SchemaPerTenant,
}
