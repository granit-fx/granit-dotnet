using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Configuration shape for <see cref="Extensions.WebhooksEntityFrameworkCoreHostApplicationBuilderExtensions
/// .AddGranitWebhooksEntityFrameworkCore"/>. Carries the dual-scope storage choice and the
/// EF Core <see cref="DbContextOptionsBuilder"/> callbacks per context.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063, Webhooks is a dual-scope module: platform-managed subscriptions
/// (host SIEM forwards for SOC2) coexist with tenant-managed subscriptions (per-tenant
/// self-service). <see cref="StorageMode"/> selects how the two are physically laid out.
/// </para>
/// <para>
/// <b>Shared (default)</b> — single host table; tenant rows carry a <c>TenantId</c>
/// and are filtered by a row-level query filter. Backwards compatible with pre-ADR-063
/// deployments. Only <see cref="Configure"/> is consulted.
/// </para>
/// <para>
/// <b>Segregated</b> — host rows live in a host-pinned context; tenant rows live in an
/// isolated tenant context (schema-per-tenant or database-per-tenant). Provides
/// defense-in-depth against row-level filter regressions and native
/// <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage for RGPD Art. 17. Requires
/// <see cref="ConfigureHost"/> for the host context and at least one of the tenant-side
/// configurations for the isolated context. <b>Implementation pending — Phase 2B of #2377.</b>
/// </para>
/// </remarks>
public sealed class WebhooksEntityFrameworkCoreOptions
{
    /// <summary>
    /// The dual-scope storage layout — see <see cref="DualScopeStorageMode"/>.
    /// </summary>
    public DualScopeStorageMode StorageMode { get; set; } = DualScopeStorageMode.Shared;

    /// <summary>
    /// EF Core configuration for the single shared <c>WebhooksDbContext</c>.
    /// Consulted when <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Shared"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }

    /// <summary>
    /// EF Core configuration for the host-pinned context that holds platform-managed
    /// subscriptions. Consulted when <see cref="StorageMode"/> is
    /// <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureHost { get; set; }

    /// <summary>
    /// EF Core configuration for the tenant-isolated context under
    /// <c>TenantIsolationStrategy.SchemaPerTenant</c>. The string argument is the active
    /// tenant schema name. Consulted when <see cref="StorageMode"/> is
    /// <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder, string>? ConfigureSchemaPerTenant { get; set; }

    /// <summary>
    /// EF Core configuration for the tenant-isolated context under
    /// <c>TenantIsolationStrategy.DatabasePerTenant</c>. Consulted when
    /// <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureDatabasePerTenant { get; set; }
}
