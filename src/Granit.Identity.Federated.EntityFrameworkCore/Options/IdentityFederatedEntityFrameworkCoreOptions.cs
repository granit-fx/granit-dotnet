using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Federated.EntityFrameworkCore.Options;

/// <summary>
/// Configuration shape for <see cref="Extensions.IdentityFederatedEntityFrameworkCoreHostApplicationBuilderExtensions
/// .AddGranitIdentityFederatedEntityFrameworkCore"/>. Carries the dual-scope storage
/// choice and the EF Core <see cref="DbContextOptionsBuilder"/> callbacks per context.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063, Identity.Federated is a dual-scope module: host-federated identities
/// (host admins via the IDP) coexist with tenant-federated identities (per-tenant SSO).
/// <see cref="StorageMode"/> selects how the two are physically laid out.
/// </para>
/// <para>
/// <b>Shared (default)</b> — single host table; tenant rows carry a <c>TenantId</c> and
/// are filtered by a row-level query filter. Backwards compatible with the Phase A
/// shape (#2406). Only <see cref="Configure"/> is consulted.
/// </para>
/// <para>
/// <b>Segregated</b> — host rows live in a host-pinned context; tenant rows live in an
/// isolated tenant context (schema-per-tenant or database-per-tenant). Per-tenant SSO
/// revocation by <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> becomes the GDPR Art. 17
/// primitive. Requires <see cref="ConfigureHost"/> for the host context and at least one
/// of the tenant-side configurations for the isolated context.
/// </para>
/// </remarks>
public sealed class IdentityFederatedEntityFrameworkCoreOptions
{
    /// <summary>
    /// The dual-scope storage layout — see <see cref="DualScopeStorageMode"/>.
    /// </summary>
    public DualScopeStorageMode StorageMode { get; set; } = DualScopeStorageMode.Shared;

    /// <summary>
    /// EF Core configuration for the single shared <c>IdentityFederatedHostDbContext</c>.
    /// Consulted when <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Shared"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }

    /// <summary>
    /// EF Core configuration for the host-pinned <c>IdentityFederatedHostDbContext</c>
    /// under <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureHost { get; set; }

    /// <summary>
    /// EF Core configuration for the tenant-isolated <c>IdentityFederatedTenantDbContext</c>
    /// under <c>TenantIsolationStrategy.SchemaPerTenant</c>. The active tenant schema is
    /// set per request by <c>TenantSchemaConnectionInterceptor</c> — this callback only
    /// supplies the provider + base connection string.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureSchemaPerTenant { get; set; }

    /// <summary>
    /// EF Core configuration for the tenant-isolated <c>IdentityFederatedTenantDbContext</c>
    /// under <c>TenantIsolationStrategy.DatabasePerTenant</c>. The string argument is the
    /// per-tenant connection string resolved via <c>ITenantConnectionStringProvider</c>.
    /// </summary>
    public Action<DbContextOptionsBuilder, string>? ConfigureDatabasePerTenant { get; set; }
}
