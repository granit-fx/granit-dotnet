using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the Granit.Taxonomy EF Core module.
/// </summary>
/// <remarks>
/// Set these at application startup, before <c>ConfigureServices</c> completes.
/// EF Core caches the compiled model after first use; later mutations have no effect.
/// </remarks>
public static class GranitTaxonomyDbProperties
{
    /// <summary>Table name prefix for all <c>Granit.Taxonomy</c> tables. Default: <c>"taxonomy_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "taxonomy_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for taxonomy tables. Falls back to <see cref="GranitDbDefaults.DbSchema"/>
    /// when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
