namespace Granit.Metering.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Metering EF Core module.
/// </summary>
public static class GranitMeteringDbProperties
{
    /// <summary>Table name prefix. Default: <c>"metering_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "metering_";

    /// <summary>Database schema. Default: <c>null</c> (provider default).</summary>
    public static string? DbSchema { get; set; }
}
