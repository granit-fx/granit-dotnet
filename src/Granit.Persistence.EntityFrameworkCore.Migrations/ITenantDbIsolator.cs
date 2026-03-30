using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Prepares a <see cref="DbContext"/> to operate within the scope of a specific tenant
/// before the batch migration delegate is invoked.
/// </summary>
/// <remarks>
/// Behavior varies by multi-tenancy topology:
/// <list type="bullet">
///   <item>
///     <term>Shared database (TenantId column)</term>
///     <description>
///     No-op. EF Core global query filters (<c>WHERE TenantId = @id</c>) are already
///     active via <c>TenantContextBehavior</c>.
///     </description>
///   </item>
///   <item>
///     <term>Tenant-per-Schema</term>
///     <description>
///     Executes <c>SET search_path = schema_{tenantId}</c> on the DbContext connection.
///     Must be applied before any query runs on the context.
///     </description>
///   </item>
///   <item>
///     <term>Tenant-per-Database</term>
///     <description>
///     No-op. <c>PerTenantDbContextFactory</c> has already resolved the correct
///     database connection from <c>ICurrentTenant</c>.
///     </description>
///   </item>
/// </list>
/// The default registration is <c>NullTenantDbIsolator</c> (no-op), suitable for
/// Shared and Tenant-per-Database topologies. Applications using Tenant-per-Schema
/// must register their own implementation <b>before</b> calling
/// <c>AddGranitPersistenceMigrations</c>.
/// </remarks>
public interface ITenantDbIsolator
{
    /// <summary>
    /// Configures <paramref name="context"/> for the given <paramref name="tenantId"/>
    /// before the batch delegate executes.
    /// </summary>
    Task IsolateAsync(DbContext context, Guid tenantId, CancellationToken cancellationToken);
}
