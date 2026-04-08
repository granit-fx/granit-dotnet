using Granit.Persistence.EntityFrameworkCore;

namespace Granit.MultiTenancy.EntityFrameworkCore;

/// <summary>
/// Configurable table naming for the multi-tenancy module.
/// Must be set before <c>ConfigureServices</c> completes (EF Core caches the model).
/// </summary>
public static class MultiTenancyDbProperties
{
    /// <summary>
    /// Prefix for all multi-tenancy tables. Default: <c>"tenants_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "tenants_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for host-level tables.
    /// Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
