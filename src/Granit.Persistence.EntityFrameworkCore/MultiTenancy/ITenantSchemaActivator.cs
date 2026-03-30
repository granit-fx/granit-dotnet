using System.Data.Common;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Activates a tenant schema on a database connection immediately after it is opened.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction decouples schema-based tenant isolation from any specific database provider.
/// Granit ships three built-in implementations:
/// </para>
/// <list type="bullet">
///   <item><see cref="PostgresqlTenantSchemaActivator"/> (default) — <c>SET search_path TO "schema", public</c></item>
///   <item><see cref="MySqlTenantSchemaActivator"/> — <c>USE `schema`</c> (MySQL / MariaDB)</item>
///   <item><see cref="OracleTenantSchemaActivator"/> — <c>ALTER SESSION SET CURRENT_SCHEMA = "schema"</c></item>
/// </list>
/// <para>
/// <strong>Unsupported providers</strong> — The following do not support session-level schema
/// switching; use <c>DatabasePerTenant</c> or <c>SharedDatabase</c> instead:
/// </para>
/// <list type="bullet">
///   <item><strong>SQL Server</strong> — no session-level <c>SET SCHEMA</c> equivalent.</item>
///   <item><strong>SQLite</strong> — no schema concept (single-file database).</item>
///   <item><strong>Azure Cosmos DB</strong> — NoSQL document store; isolate via container or
///         partition key (<c>TenantId</c>), not schemas.</item>
/// </list>
/// <para>
/// The default is <see cref="PostgresqlTenantSchemaActivator"/>, registered via
/// <see cref="Extensions.PersistenceTenantExtensions"/> with <c>TryAddSingleton</c>.
/// To use a different provider, register your <see cref="ITenantSchemaActivator"/>
/// before calling <c>AddTenantPerSchemaDbContext</c>.
/// </para>
/// <para>
/// <strong>Connection pool safety (critical)</strong> — implementations MUST execute the
/// schema switch unconditionally on every call. Pooled connections retain the previous
/// tenant's schema; skipping activation would cause a cross-tenant data breach (ISO 27001).
/// </para>
/// </remarks>
public interface ITenantSchemaActivator
{
    /// <summary>
    /// Activates the specified schema on the given connection (synchronous path).
    /// </summary>
    /// <param name="connection">The opened database connection.</param>
    /// <param name="schemaName">
    /// The validated schema name returned by <see cref="ITenantSchemaProvider"/>.
    /// </param>
    void ActivateSchema(DbConnection connection, string schemaName);

    /// <summary>
    /// Activates the specified schema on the given connection (asynchronous path).
    /// </summary>
    /// <param name="connection">The opened database connection.</param>
    /// <param name="schemaName">
    /// The validated schema name returned by <see cref="ITenantSchemaProvider"/>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ActivateSchemaAsync(
        DbConnection connection,
        string schemaName,
        CancellationToken cancellationToken = default);
}
