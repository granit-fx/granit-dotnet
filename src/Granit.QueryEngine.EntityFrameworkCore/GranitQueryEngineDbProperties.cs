namespace Granit.QueryEngine.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the QueryEngine EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitQueryEngineDbProperties
{
    /// <summary>
    /// Table name prefix for all QueryEngine tables. Default: <c>"query_engine_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "query_engine_";

    /// <summary>
    /// Database schema for all QueryEngine tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
