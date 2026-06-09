using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Indexing EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitIndexingDbProperties
{
    /// <summary>
    /// Table name prefix for all indexing tables. Default: <c>"indexing_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "indexing_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for indexing tables.
    /// Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
