using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Templating EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Templating is a <b>dual-scope</b> module: system templates shipped by the host coexist
/// with tenant-defined overrides. Tables live in the <b>host schema</b> and tenant
/// isolation is enforced via the <c>TenantId</c> row-level query filter (with
/// <c>EfStoreBase</c> bypass for host context).
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitTemplatingDbProperties
{
    /// <summary>
    /// Table name prefix for all templating tables. Default: <c>"templating_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "templating_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for templating tables.
    /// Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
