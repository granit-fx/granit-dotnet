namespace Granit.Invoicing.EntityFrameworkCore;

/// <summary>Table-naming properties for the Invoicing EF Core module.</summary>
public static class GranitInvoicingDbProperties
{
    /// <summary>Table prefix. Default: <c>"invoicing_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "invoicing_";

    /// <summary>Schema. Default: <c>null</c>.</summary>
    public static string? DbSchema { get; set; }
}
