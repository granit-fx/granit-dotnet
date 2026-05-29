using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Options;

/// <summary>
/// Configuration shape for
/// <c>AuditingEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitAuditingEntityFrameworkCore</c>.
/// Carries the dual-scope storage choice and the EF Core
/// <see cref="DbContextOptionsBuilder"/> callbacks per context.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063, Granit.Auditing is a dual-scope module: platform-level audit entries
/// (host-admin actions, SOC2 oversight, <c>TenantId == null</c>) coexist with per-tenant
/// audit entries (user actions, business writes).
/// </para>
/// <para>
/// <b>Shared (default)</b> — single host table; tenant rows carry a <c>TenantId</c> and
/// are filtered by a row-level query filter. Backwards compatible with pre-ADR-063
/// deployments. Only <see cref="Configure"/> is consulted.
/// </para>
/// <para>
/// <b>Segregated</b> — host rows in <c>AuditingHostDbContext</c>, tenant rows in
/// <c>AuditingTenantDbContext</c>. Cross-tenant SOC2 read queries (host-admin scope) are
/// materialised across host + every tenant via <c>ITenantsAccessor</c> +
/// <c>ICurrentTenant.Change</c>. Tenant offboarding can use native
/// <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage for RGPD Art. 17.
/// </para>
/// </remarks>
public sealed class AuditingEntityFrameworkCoreOptions
{
    /// <summary>The dual-scope storage layout — see <see cref="DualScopeStorageMode"/>.</summary>
    public DualScopeStorageMode StorageMode { get; set; } = DualScopeStorageMode.Shared;

    /// <summary>
    /// EF Core configuration for the single shared <c>AuditingHostDbContext</c>.
    /// Consulted when <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Shared"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }

    /// <summary>
    /// EF Core configuration for the host-pinned context. Consulted when
    /// <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureHost { get; set; }

    /// <summary>
    /// EF Core configuration for <c>AuditingTenantDbContext</c> under
    /// <c>TenantIsolationStrategy.SchemaPerTenant</c>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureSchemaPerTenant { get; set; }

    /// <summary>
    /// EF Core configuration for <c>AuditingTenantDbContext</c> under
    /// <c>TenantIsolationStrategy.DatabasePerTenant</c>. The string argument is the
    /// per-tenant connection string.
    /// </summary>
    public Action<DbContextOptionsBuilder, string>? ConfigureDatabasePerTenant { get; set; }
}
