using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Options;

/// <summary>
/// Configuration shape for
/// <c>IdentityEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitIdentityEntityFrameworkCore</c>.
/// Carries the dual-scope storage choice and the EF Core
/// <see cref="DbContextOptionsBuilder"/> callbacks per context.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063, Granit.Identity is a dual-scope module: host-admin users (support / SRE
/// accounts, <c>TenantId == null</c>) coexist with tenant users. <see cref="StorageMode"/>
/// selects how the two are physically laid out.
/// </para>
/// <para>
/// <b>Shared (default)</b> — single host table; tenant rows carry a <c>TenantId</c> and
/// are filtered by a row-level query filter. Backwards compatible with pre-ADR-063
/// deployments. Only <see cref="Configure"/> is consulted.
/// </para>
/// <para>
/// <b>Segregated</b> — host rows live in a host-pinned context; tenant rows live in an
/// isolated tenant context (schema-per-tenant or database-per-tenant). Provides
/// defense-in-depth against row-level filter regressions and native
/// <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage for RGPD Art. 17 — high-value for
/// IAM stores given the cross-tenant impersonation surface a filter leak exposes.
/// Requires <see cref="ConfigureHost"/> plus at least one of the tenant-side configurations.
/// </para>
/// </remarks>
public sealed class IdentityEntityFrameworkCoreOptions
{
    /// <summary>
    /// The dual-scope storage layout — see <see cref="DualScopeStorageMode"/>.
    /// </summary>
    public DualScopeStorageMode StorageMode { get; set; } = DualScopeStorageMode.Shared;

    /// <summary>
    /// EF Core configuration for the single shared <c>IdentityHostDbContext</c>.
    /// Consulted when <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Shared"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }

    /// <summary>
    /// EF Core configuration for the host-pinned context that holds host-admin users.
    /// Consulted when <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureHost { get; set; }

    /// <summary>
    /// EF Core configuration for <c>IdentityTenantDbContext</c> under
    /// <c>TenantIsolationStrategy.SchemaPerTenant</c>. The active tenant schema is set per
    /// request by <c>TenantSchemaConnectionInterceptor</c> — this callback only supplies
    /// the provider + base connection string.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureSchemaPerTenant { get; set; }

    /// <summary>
    /// EF Core configuration for <c>IdentityTenantDbContext</c> under
    /// <c>TenantIsolationStrategy.DatabasePerTenant</c>. The string argument is the
    /// per-tenant connection string resolved via <c>ITenantConnectionStringProvider</c>.
    /// </summary>
    public Action<DbContextOptionsBuilder, string>? ConfigureDatabasePerTenant { get; set; }
}
