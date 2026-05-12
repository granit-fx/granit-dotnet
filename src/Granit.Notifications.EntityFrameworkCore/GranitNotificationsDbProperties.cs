using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Notifications.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Notifications EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Notifications is a <b>dual-scope</b> module: host-defined notification channels and
/// templates coexist with tenant-scoped subscriptions, preferences, and delivery logs.
/// Tables live in the <b>host schema</b> and tenant isolation is enforced via the
/// <c>TenantId</c> row-level query filter (with <c>EfStoreBase</c> bypass for host context).
/// </para>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.Notifications.EntityFrameworkCore</c>. Both the internal
/// <c>NotificationsDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitNotificationsDbProperties
{
    /// <summary>
    /// Table name prefix for all notifications tables. Default: <c>"notifications_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "notifications_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for notification tables.
    /// Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
