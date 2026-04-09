using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore;

/// <summary>Table-naming properties for Payments EF Core module.</summary>
public static class GranitPaymentsDbProperties
{
    public static string DbTablePrefix { get; set; } = "payments_";

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
