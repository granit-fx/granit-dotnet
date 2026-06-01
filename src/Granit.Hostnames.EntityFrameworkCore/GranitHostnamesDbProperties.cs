using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the Hostnames EF Core module.
/// </summary>
/// <remarks>
/// Set at application startup, before <c>ConfigureServices</c> completes — EF Core caches
/// the compiled model after first use, so later mutations have no effect.
/// </remarks>
public static class GranitHostnamesDbProperties
{
    /// <summary>Table name prefix for all hostname tables. Default: <c>"hostname_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "hostname_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema. Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
