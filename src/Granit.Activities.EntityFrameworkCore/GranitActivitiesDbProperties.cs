using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Activities.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the Activities EF Core module
/// (mirrors <see cref="Timeline.EntityFrameworkCore.GranitTimelineDbProperties"/>).
/// </summary>
/// <remarks>
/// Set these at application startup, before <c>ConfigureServices</c> completes.
/// EF Core caches the compiled model after first DbContext use — later
/// mutations are silently ignored.
/// </remarks>
public static class GranitActivitiesDbProperties
{
    /// <summary>Table-name prefix for all activity tables. Default: <c>"activities_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "activities_";

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
