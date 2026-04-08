using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Subscriptions EF Core module.
/// </summary>
public static class GranitSubscriptionsDbProperties
{
    /// <summary>Table name prefix. Default: <c>"subscriptions_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "subscriptions_";

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
