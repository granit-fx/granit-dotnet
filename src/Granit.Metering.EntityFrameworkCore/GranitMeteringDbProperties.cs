using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Metering.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Metering EF Core module.
/// </summary>
public static class GranitMeteringDbProperties
{
    /// <summary>Table name prefix. Default: <c>"metering_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "metering_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for tenant-level tables.
    /// Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
