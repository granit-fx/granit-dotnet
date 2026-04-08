using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore;

/// <summary>Table-naming properties for the Privacy EF Core module.</summary>
public static class GranitPrivacyDbProperties
{
    /// <summary>Table prefix. Default: <c>"privacy_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "privacy_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for tenant-level tables.
    /// Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
