namespace Granit.Templating.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Templating EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitTemplatingDbProperties
{
    /// <summary>
    /// Table name prefix for all templating tables. Default: <c>"templating_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "templating_";

    /// <summary>
    /// Database schema for all templating tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
