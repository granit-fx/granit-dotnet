namespace Granit.Tax.EntityFrameworkCore;

/// <summary>Configurable table-naming properties for the Tax EF Core module.</summary>
public static class GranitTaxDbProperties
{
    /// <summary>Table name prefix. Default: <c>"tax_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "tax_";

    /// <summary>Database schema. Default: <c>null</c> (provider default).</summary>
    public static string? DbSchema { get; set; }
}
