using Granit.Persistence.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore;

/// <summary>Database table naming properties for the CustomerBalance module.</summary>
public static class GranitCustomerBalanceDbProperties
{
    /// <summary>Table prefix. Default: <c>"customer_balance_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "customer_balance_";

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
