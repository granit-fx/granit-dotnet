using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Bff.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the BFF EF Core module.
/// </summary>
/// <remarks>
/// Set these properties at application startup, before <c>ConfigureServices</c> completes.
/// EF Core caches the compiled model after first use — later mutations have no effect.
/// </remarks>
public static class GranitBffDbProperties
{
    /// <summary>
    /// Table name prefix for all BFF tables. Default: <c>"bff_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "bff_";

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
