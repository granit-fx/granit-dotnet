using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore.Options;

/// <summary>
/// Configuration shape for
/// <c>NotificationsEntityFrameworkCoreHostApplicationBuilderExtensions.AddGranitNotificationsEntityFrameworkCore</c>.
/// Carries the dual-scope storage choice and the EF Core
/// <see cref="DbContextOptionsBuilder"/> callbacks per context.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-063, Granit.Notifications is a dual-scope module: platform-level notifications
/// (host announcements, SOC2 broadcasts, <c>TenantId == null</c>) coexist with per-tenant
/// notifications (user inboxes, preferences, push tokens). <see cref="StorageMode"/>
/// selects how the two are physically laid out.
/// </para>
/// <para>
/// <b>Shared (default)</b> — single host table; tenant rows carry a <c>TenantId</c> and
/// are filtered by a row-level query filter. Backwards compatible with pre-ADR-063
/// deployments. Only <see cref="Configure"/> is consulted.
/// </para>
/// <para>
/// <b>Segregated</b> — host rows live in a host-pinned context; tenant rows live in an
/// isolated tenant context. Provides native <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c>
/// lessivage for RGPD Art. 17 — notification bodies carry PII (message text, recipient
/// metadata).
/// </para>
/// </remarks>
public sealed class NotificationsEntityFrameworkCoreOptions
{
    /// <summary>The dual-scope storage layout — see <see cref="DualScopeStorageMode"/>.</summary>
    public DualScopeStorageMode StorageMode { get; set; } = DualScopeStorageMode.Shared;

    /// <summary>
    /// EF Core configuration for the single shared <c>NotificationsHostDbContext</c>.
    /// Consulted when <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Shared"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? Configure { get; set; }

    /// <summary>
    /// EF Core configuration for the host-pinned context. Consulted when
    /// <see cref="StorageMode"/> is <see cref="DualScopeStorageMode.Segregated"/>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureHost { get; set; }

    /// <summary>
    /// EF Core configuration for <c>NotificationsTenantDbContext</c> under
    /// <c>TenantIsolationStrategy.SchemaPerTenant</c>.
    /// </summary>
    public Action<DbContextOptionsBuilder>? ConfigureSchemaPerTenant { get; set; }

    /// <summary>
    /// EF Core configuration for <c>NotificationsTenantDbContext</c> under
    /// <c>TenantIsolationStrategy.DatabasePerTenant</c>. The string argument is the
    /// per-tenant connection string.
    /// </summary>
    public Action<DbContextOptionsBuilder, string>? ConfigureDatabasePerTenant { get; set; }
}
