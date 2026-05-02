using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Entities.Customization.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the Entities.Customization EF Core
/// module (mirrors <see cref="Activities.EntityFrameworkCore.GranitActivitiesDbProperties"/>).
/// </summary>
/// <remarks>
/// Set these at application startup, before <c>ConfigureServices</c> completes.
/// EF Core caches the compiled model after first DbContext use — later mutations
/// are silently ignored.
/// </remarks>
public static class GranitEntitiesCustomizationDbProperties
{
    /// <summary>Table-name prefix for all entity-customization tables. Default: <c>"entities_customization_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "entities_customization_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for tenant-level tables. Falls back to
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
