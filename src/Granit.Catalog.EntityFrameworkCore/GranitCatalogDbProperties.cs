using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Catalog.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Catalog EF Core module.
/// </summary>
public static class GranitCatalogDbProperties
{
    /// <summary>Table name prefix. Default: <c>"catalog_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "catalog_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for catalog tables.
    /// Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    /// <remarks>
    /// MVP scope: catalog is Host-owned (HostDbSchema). When the e-commerce phase
    /// introduces tenant-scoped products, this default may need adjustment per
    /// the path chosen in ADR 032.
    /// </remarks>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
