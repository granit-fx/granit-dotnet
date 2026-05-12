using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Timeline EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Timeline is a <b>dual-scope</b> module: it captures a platform-wide activity stream
/// across tenants for host-admin observability as well as tenant-scoped activity feeds.
/// Tables live in the <b>host schema</b> and tenant isolation is enforced via the
/// <c>TenantId</c> row-level query filter (with <c>EfStoreBase</c> bypass for host context).
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitTimelineDbProperties
{
    /// <summary>
    /// Table name prefix for all timeline tables. Default: <c>"timeline_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "timeline_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for Timeline tables. Falls back to
    /// <see cref="GranitDbDefaults.HostDbSchema"/>, then <see cref="GranitDbDefaults.DbSchema"/>
    /// when not explicitly set — Timeline tables intentionally live in the host schema
    /// (row-level multi-tenancy via <c>TenantId</c> filter).
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
