using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore;

/// <summary>Table-naming properties for the Invoicing EF Core module.</summary>
public static class GranitInvoicingDbProperties
{
    /// <summary>Table prefix. Default: <c>"invoicing_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "invoicing_";

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
