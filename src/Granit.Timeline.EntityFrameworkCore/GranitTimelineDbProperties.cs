namespace Granit.Timeline.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Timeline EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitTimelineDbProperties
{
    /// <summary>
    /// Table name prefix for all timeline tables. Default: <c>"timeline_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "timeline_";

    /// <summary>
    /// Database schema for all timeline tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
