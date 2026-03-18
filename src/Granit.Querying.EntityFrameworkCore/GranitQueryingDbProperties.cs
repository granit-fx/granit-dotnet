namespace Granit.Querying.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Querying EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitQueryingDbProperties
{
    /// <summary>
    /// Table name prefix for all querying tables. Default: <c>"querying_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "querying_";

    /// <summary>
    /// Database schema for all querying tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
